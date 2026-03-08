#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = ROOT / "fixtures" / "exploratory-downloads" / "catalog.json"
OLD_PROJECT = ROOT / "reference-src" / "current-syncfusion-tool" / "PdfFormFiller.Cli" / "PdfFormFiller.Cli.csproj"
NEW_PROJECT = ROOT / "src" / "PdfFormFiller.Cli" / "PdfFormFiller.Cli.csproj"
OLD_DLL = ROOT / "reference-src" / "current-syncfusion-tool" / "PdfFormFiller.Cli" / "bin" / "Debug" / "net10.0" / "pdf-form-filler.dll"
NEW_DLL = ROOT / "src" / "PdfFormFiller.Cli" / "bin" / "Debug" / "net10.0" / "pdf-form-filler.dll"
DEFAULT_OUTPUT_ROOT = ROOT / "notes" / "generated" / "xfa-experimental"


@dataclass
class CliResult:
    exit_code: int
    stdout: str
    stderr: str


def main() -> int:
    parser = argparse.ArgumentParser(description="Run side-by-side experimental XFA fill comparisons.")
    parser.add_argument("--limit", type=int, default=None, help="Limit the number of XFA fixtures to process.")
    parser.add_argument("--ids", nargs="*", default=None, help="Specific fixture ids to process.")
    parser.add_argument(
        "--output-root",
        default=str(DEFAULT_OUTPUT_ROOT),
        help="Directory root for generated comparison runs.",
    )
    args = parser.parse_args()

    syncfusion_key = resolve_syncfusion_license_key()
    if not syncfusion_key:
        raise SystemExit("Missing SYNCFUSION_LICENSE_KEY or .env.local entry.")

    ensure_built(NEW_PROJECT)
    ensure_built(OLD_PROJECT)

    catalog = json.loads(CATALOG_PATH.read_text())
    fixtures = [entry for entry in catalog if entry["expectation"] == "new_rejects_xfa"]

    if args.ids:
        ids = set(args.ids)
        fixtures = [entry for entry in fixtures if entry["id"] in ids]

    fixtures.sort(key=lambda entry: entry["relativePath"].lower())
    if args.limit is not None:
        fixtures = fixtures[: args.limit]

    if not fixtures:
        raise SystemExit("No XFA fixtures selected.")

    run_root = Path(args.output_root) / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run_root.mkdir(parents=True, exist_ok=True)

    summary: dict[str, Any] = {
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "fixtureCount": len(fixtures),
        "results": [],
    }

    for fixture in fixtures:
        fixture_result = run_fixture(fixture, run_root, syncfusion_key)
        summary["results"].append(fixture_result)
        print(
            f"{fixture['id']}: "
            f"old_exit={fixture_result['oldFill']['exitCode']} "
            f"new_exit={fixture_result['newFill']['exitCode']} "
            f"old_cli_field_match={fixture_result['comparison']['oldCliFieldMatch']} "
            f"new_cli_field_match={fixture_result['comparison']['newCliFieldMatch']} "
            f"old_cli_classification_match={fixture_result['comparison']['oldCliClassificationMatch']} "
            f"new_cli_classification_match={fixture_result['comparison']['newCliClassificationMatch']}"
        )

    (run_root / "summary.json").write_text(json.dumps(summary, indent=2))
    print(f"\nArtifacts written to {run_root}")
    return 0


def run_fixture(fixture: dict[str, Any], run_root: Path, syncfusion_key: str) -> dict[str, Any]:
    pdf_path = ROOT / "fixtures" / "exploratory-downloads" / fixture["relativePath"]
    fixture_root = run_root / fixture["id"]
    fixture_root.mkdir(parents=True, exist_ok=True)

    input_copy = fixture_root / "input.pdf"
    shutil.copy2(pdf_path, input_copy)

    old_original = run_cli(OLD_DLL, ["inspect", "--pdf", str(pdf_path), "--json"], syncfusion_key)
    new_original = run_cli(NEW_DLL, ["inspect", "--pdf", str(pdf_path), "--json"])
    old_original_json = parse_json(old_original.stdout)
    new_original_json = parse_json(new_original.stdout)

    values = generate_values(old_original_json)
    values_path = fixture_root / "generated-values.json"
    values_path.write_text(json.dumps(values, indent=2))

    old_output = fixture_root / "old-filled.pdf"
    new_output = fixture_root / "new-filled-experimental-xfa.pdf"

    old_fill = run_cli(
        OLD_DLL,
        ["fill", "--pdf", str(pdf_path), "--values", str(values_path), "--out", str(old_output), "--json"],
        syncfusion_key,
    )
    new_fill = run_cli(
        NEW_DLL,
        [
            "fill",
            "--pdf",
            str(pdf_path),
            "--values",
            str(values_path),
            "--out",
            str(new_output),
            "--experimental-xfa",
            "--json",
        ],
    )

    write_text_if_present(fixture_root / "old-fill-result.json", old_fill.stdout)
    write_text_if_present(fixture_root / "new-fill-result.json", new_fill.stdout)

    old_inspect_old_output = run_cli(OLD_DLL, ["inspect", "--pdf", str(old_output), "--json"], syncfusion_key)
    old_inspect_new_output = run_cli(OLD_DLL, ["inspect", "--pdf", str(new_output), "--json"], syncfusion_key)
    new_inspect_old_output = run_cli(NEW_DLL, ["inspect", "--pdf", str(old_output), "--json"])
    new_inspect_new_output = run_cli(NEW_DLL, ["inspect", "--pdf", str(new_output), "--json"])

    write_text_if_present(fixture_root / "old-inspect-old-output.json", old_inspect_old_output.stdout)
    write_text_if_present(fixture_root / "old-inspect-new-output.json", old_inspect_new_output.stdout)
    write_text_if_present(fixture_root / "new-inspect-old-output.json", new_inspect_old_output.stdout)
    write_text_if_present(fixture_root / "new-inspect-new-output.json", new_inspect_new_output.stdout)

    render_pages(old_output, fixture_root / "old-page")
    render_pages(new_output, fixture_root / "new-page")
    create_compare_images(fixture_root)

    old_fill_json = safe_parse_json(old_fill.stdout)
    new_fill_json = safe_parse_json(new_fill.stdout)
    old_old_json = parse_json(old_inspect_old_output.stdout)
    old_new_json = parse_json(old_inspect_new_output.stdout)
    new_old_json = parse_json(new_inspect_old_output.stdout)
    new_new_json = parse_json(new_inspect_new_output.stdout)

    comparison = {
        "oldCliFieldMatch": normalize_field_parity(old_old_json) == normalize_field_parity(old_new_json),
        "newCliFieldMatch": normalize_field_parity(new_old_json) == normalize_field_parity(new_new_json),
        "oldCliClassificationMatch": summarize_inspection(old_old_json) == summarize_inspection(old_new_json),
        "newCliClassificationMatch": summarize_inspection(new_old_json) == summarize_inspection(new_new_json),
        "fillCountMatch": normalize_fill_result(old_fill_json) == normalize_fill_result(new_fill_json),
    }

    comparison_path = fixture_root / "comparison.json"
    comparison_path.write_text(
        json.dumps(
            {
                "fixture": fixture["id"],
                "oldOriginalInspect": summarize_inspection(old_original_json),
                "newOriginalInspect": summarize_inspection(new_original_json),
                "oldFill": summarize_cli_result(old_fill, old_fill_json),
                "newFill": summarize_cli_result(new_fill, new_fill_json),
                "comparison": comparison,
            },
            indent=2,
        )
    )

    return {
        "id": fixture["id"],
        "relativePath": fixture["relativePath"],
        "artifactDir": os.path.relpath(fixture_root, ROOT),
        "oldFill": summarize_cli_result(old_fill, old_fill_json),
        "newFill": summarize_cli_result(new_fill, new_fill_json),
        "comparison": comparison,
    }


def generate_values(inspection: dict[str, Any]) -> dict[str, Any]:
    values: dict[str, Any] = {}
    text_index = 1
    for field in inspection.get("Fields", []):
        if field.get("ReadOnly"):
            continue
        kind = field.get("Kind")
        name = field.get("Name")
        if kind == "checkbox":
            values[name] = choose_sample_checkbox_value(field)
        elif kind in {"radio", "combo", "list"}:
            choices = [choice for choice in field.get("Choices", []) if str(choice).strip()]
            if choices:
                values[name] = choices[0]
        elif kind == "text":
            sample_value = choose_sample_text_value(field, text_index)
            if sample_value is not None:
                values[name] = sample_value
                text_index += 1
    return values


def choose_sample_text_value(field: dict[str, Any], text_index: int) -> str | None:
    name = str(field.get("Name", ""))
    tooltip = str(field.get("ToolTip", "") or "")
    haystack = f"{name} {tooltip}".lower()
    name_lower = name.lower()

    if "sg_p2_s8_apt" in name_lower:
        additional_pay_types = ["ND", "SP", "HP", "SUB", "QTR", "OT"]
        return additional_pay_types[(text_index - 1) % len(additional_pay_types)]
    if "nf2__p2_s8_ap" in name_lower or "nf2__p2_s8_bp" in name_lower:
        return "50"
    if "sg_p2_s8_apper" in name_lower or "sg_p2_s8_per" in name_lower:
        return "hr"
    if "sg_p2_s8_gr" in name_lower:
        return "11"
    if "sg_p2_s8_st" in name_lower:
        return "4"
    if "ni__p2_" in name_lower or "ni__p1_sun1" in name_lower:
        return "8"
    if "sg_p2_cls" in name_lower:
        return "GS"
    if "sg_p2_code" in name_lower:
        return "55"
    if "txt_p2_s13ex" in name_lower:
        return "D"
    if "txt_p2_s13_rem" in name_lower:
        return "Modified duty"
    if "sg_p1_s15" in name_lower or "sg_p2_nag" in name_lower:
        return "Alex Doe"
    if "sg_p2_tit" in name_lower or "sg_p2_titsig" in name_lower:
        return "HR Mgr"

    if "foreign address only" in haystack:
        return None
    if "alien registration number" in haystack or "a-number" in haystack or "anumber" in haystack or "aliennumber" in haystack:
        return "A1234567"
    if "date" in haystack or "mm/dd/yyyy" in haystack:
        return "03/08/2026"
    if "total number of marriages" in haystack or "number of marriages" in haystack:
        return "2"
    if "total number of children" in haystack:
        return "3"
    if "hours per day" in haystack or "hrs per day" in haystack or "hoursperday" in haystack or "hrsperday" in haystack:
        return "8"
    if "days per week" in haystack or "daysperweek" in haystack:
        return "5"
    if "range in degrees" in haystack or "degrees" in haystack:
        return "45-65"
    if "pounds" in haystack or " lbs" in haystack or "lbs" in haystack:
        return "25"
    if "social security" in haystack or "ssn" in haystack:
        return "123-45-6789"
    if "tax identification" in haystack or "tax id" in haystack or "taxid" in haystack:
        return "12-3456789"
    if "owcp" in haystack or "file number" in haystack:
        return "A1234567"
    if "zip" in haystack or "postal" in haystack:
        return "94105"
    if "phone" in haystack or "telephone" in haystack:
        return "555-0100"
    if "email" in haystack:
        return "test@example.com"
    if "son or daughter" in haystack and "name" in haystack:
        child_names = ["Ava Doe", "Ben Doe", "Cara Doe", "Dina Doe"]
        return child_names[(text_index - 1) % len(child_names)]
    if "enter one of the following options: resides with me" in haystack or "resides with me, does not reside with me" in haystack:
        return "resides with me"
    if "biological son or daughter" in haystack or "stepchild" in haystack or "legally adopted son or daughter" in haystack:
        return "biological daughter"
    if "city" in haystack:
        return "Oakland"
    if "in care of name" in haystack:
        return "Alex Doe"
    if "apartment, suite or floor number" in haystack or "apartment suite or floor number" in haystack:
        return "12B"
    if name.startswith("form1[0].#subform[4].P7_Country"):
        return "USA"
    if "street" in haystack or "address" in haystack:
        return "123 Main St"
    if "state" in haystack:
        return "CA"
    if "first name" in haystack:
        return "Alex"
    if "last name" in haystack:
        return "Doe"
    if "middle name" in haystack or "middle initial" in haystack:
        return "Q"
    if "occupation" in haystack:
        return "Analyst"
    if "specialty" in haystack:
        return "Ortho"
    if "diagnosis" in haystack:
        return "Back strain"
    if "injury description" in haystack or "injury occurred" in haystack:
        return "Lift injury"
    if "history of the injury" in haystack or "injury history" in haystack:
        return "Seen after strain"
    if "clinical findings" in haystack:
        return "Lumbar tenderness"
    if "description" in haystack:
        return "Sample note"
    if "employee" in haystack and "name" not in haystack:
        return "Employee"
    if "employer name" in haystack or "school name" in haystack:
        options = ["Acme Co", "Bay School", "Metro Health", "Civic Center"]
        return options[(text_index - 1) % len(options)]
    if "employer" in haystack and "company" in haystack:
        return "Acme Co"

    return f"Sample {text_index}"


def choose_sample_checkbox_value(field: dict[str, Any]) -> bool:
    name = str(field.get("Name", ""))
    tooltip = str(field.get("ToolTip", "") or "")
    haystack = f"{name} {tooltip}".lower()

    if "intermittent" in haystack:
        return False
    if "continuous" in haystack:
        return True
    if "select apartment" in haystack:
        return True
    if "select suite" in haystack or "select floor" in haystack:
        return False

    return True


def normalize_fill_result(result: dict[str, Any] | None) -> dict[str, Any] | None:
    if result is None:
        return None
    return {
        "AppliedFields": result.get("AppliedFields"),
        "SkippedFields": sorted(result.get("SkippedFields", [])),
        "UnusedInputKeys": sorted(result.get("UnusedInputKeys", [])),
        "Flattened": result.get("Flattened"),
    }


def normalize_field_parity(inspection: dict[str, Any]) -> dict[str, Any]:
    def normalize_kind(kind: str) -> str:
        return kind if kind in {"text", "checkbox", "combo", "list", "radio", "signature"} else "unsupported"

    normalized_fields = []
    for field in inspection.get("Fields", []):
        current = field.get("CurrentValue")
        if field.get("ReadOnly") and normalize_kind(field.get("Kind", "")) == "text" and current not in (None, ""):
            current = "<read-only-text>"
        normalized_fields.append(
            {
                "Name": field.get("Name"),
                "Kind": normalize_kind(field.get("Kind", "")),
                "ToolTip": field.get("ToolTip"),
                "ReadOnly": field.get("ReadOnly"),
                "Required": field.get("Required"),
                "CurrentValue": current,
                "Choices": [choice.rstrip() for choice in field.get("Choices", []) if str(choice).strip()],
            }
        )

    return {"FieldCount": inspection.get("FieldCount"), "Fields": normalized_fields}


def summarize_inspection(inspection: dict[str, Any]) -> dict[str, Any]:
    return {
        "FormType": inspection.get("FormType"),
        "IsXfaForm": inspection.get("IsXfaForm"),
        "IsSupportedAcroForm": inspection.get("IsSupportedAcroForm"),
        "CanFillValues": inspection.get("CanFillValues"),
        "SupportedFillableFieldCount": inspection.get("SupportedFillableFieldCount"),
        "FieldCount": inspection.get("FieldCount"),
        "ValidationMessage": inspection.get("ValidationMessage"),
    }


def summarize_cli_result(cli_result: CliResult, parsed_json: dict[str, Any] | None) -> dict[str, Any]:
    result = {"exitCode": cli_result.exit_code}
    if parsed_json is not None:
        result.update(normalize_fill_result(parsed_json) or {})
    if cli_result.stderr.strip():
        result["stderr"] = cli_result.stderr.strip()
    return result


def render_pages(pdf_path: Path, output_prefix: Path) -> None:
    if not pdf_path.exists():
        return
    for existing in output_prefix.parent.glob(f"{output_prefix.name}-*.png"):
        existing.unlink()
    subprocess.run(
        [
            "pdftoppm",
            "-png",
            str(pdf_path),
            str(output_prefix),
        ],
        cwd=ROOT,
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def create_compare_images(fixture_root: Path) -> None:
    old_pages = sorted(fixture_root.glob("old-page-*.png"))
    for old_page in old_pages:
        page_suffix = old_page.stem.removeprefix("old-page-")
        new_page = fixture_root / f"new-page-{page_suffix}.png"
        compare_page = fixture_root / f"compare-page-{page_suffix}.png"
        if not new_page.exists():
            continue
        subprocess.run(
            [
                "magick",
                str(old_page),
                str(new_page),
                "+append",
                str(compare_page),
            ],
            cwd=ROOT,
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )


def resolve_syncfusion_license_key() -> str | None:
    if os.environ.get("SYNCFUSION_LICENSE_KEY"):
        return os.environ["SYNCFUSION_LICENSE_KEY"]

    env_local = ROOT / ".env.local"
    if not env_local.exists():
        return None

    for raw_line in env_local.read_text().splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("export "):
            line = line[len("export ") :].strip()
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        if key.strip() != "SYNCFUSION_LICENSE_KEY":
            continue
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
            value = value[1:-1]
        return value

    return None


def ensure_built(project_path: Path) -> None:
    subprocess.run(
        ["dotnet", "build", str(project_path), "--configuration", "Debug", "--nologo", "--verbosity", "minimal"],
        cwd=ROOT,
        check=True,
    )


def run_cli(dll_path: Path, args: list[str], syncfusion_key: str | None = None) -> CliResult:
    env = os.environ.copy()
    if syncfusion_key:
        env["SYNCFUSION_LICENSE_KEY"] = syncfusion_key
    result = subprocess.run(
        ["dotnet", str(dll_path), *args],
        cwd=ROOT,
        env=env,
        capture_output=True,
        text=True,
    )
    return CliResult(result.returncode, result.stdout, result.stderr)


def write_text_if_present(path: Path, text: str) -> None:
    if text:
        path.write_text(text)


def parse_json(text: str) -> dict[str, Any]:
    value = safe_parse_json(text)
    if value is None:
        raise ValueError(f"Unable to parse JSON: {text[:400]}")
    return value


def safe_parse_json(text: str) -> dict[str, Any] | None:
    text = text.strip()
    if not text:
        return None
    return json.loads(text)


if __name__ == "__main__":
    raise SystemExit(main())
