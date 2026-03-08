# Exploratory Downloads

This folder contains additional public-source PDFs for hardening edge cases beyond the frozen compatibility corpus.

These files are not part of the original handoff baseline.
Use them to:
- compare old vs new behavior on fresh real-world forms
- add regression tests for structural edge cases
- probe areas the baseline corpus under-represents, such as legal forms, government packets, read-only fields, and XFA

The machine-readable manifest for this folder is:
- `catalog.json`

## Legal forms

- `legal/ao240-ifp-short-form.pdf`
  - Direct PDF: `https://www.uscourts.gov/sites/default/files/ao240_0.pdf`
  - Source page: `https://www.uscourts.gov/forms-rules/forms/application-proceed-district-court-without-prepaying-fees-or-costs-short-form`
  - Why it is useful: many yes/no sections, read-only labels, signature areas, and financial disclosure blocks

- `legal/ao399-waiver-of-service.pdf`
  - Direct PDF: `https://www.uscourts.gov/sites/default/files/ao399.pdf`
  - Source page: `https://www.uscourts.gov/forms-rules/forms/waiver-service-summons`
  - Why it is useful: caption-heavy court form with address blocks and repeated court-location choice values

- `legal/ao440-civil-summons.pdf`
  - Direct PDF: `https://www.uscourts.gov/sites/default/files/ao440.pdf`
  - Source page: `https://www.uscourts.gov/forms-rules/forms/summons-civil-action`
  - Why it is useful: court caption, clerk/date/signature blocks, and push-button widgets

- `legal/js044-civil-cover-sheet.pdf`
  - Direct PDF: `https://www.uscourts.gov/sites/default/files/js044.pdf`
  - Source page: `https://www.uscourts.gov/forms-rules/forms/civil-cover-sheet`
  - Why it is useful: dense checkbox matrix and federal court classification fields

- `legal/n400-naturalization-application.pdf`
  - Direct PDF: `https://www.uscis.gov/sites/default/files/document/forms/n-400.pdf`
  - Source page: `https://www.uscis.gov/n-400`
  - Why it is useful: real XFA government form with a large field tree
  - Important note: the new implementation should reject this as XFA even though the old Syncfusion snapshot currently treats it as supported

- `legal/ao239-ifp-long-form.pdf`
  - Direct PDF: `https://www.uscourts.gov/sites/default/files/ao239_1.pdf`
  - Source page: `https://www.uscourts.gov/forms-rules/forms/application-proceed-district-court-without-prepaying-fees-or-costs-long-form`
  - Why it is useful: dense financial sections, repeated spouse fields, many yes/no boxes, and lots of court-style structure

- `legal/ao088b-subpoena-produce-documents.pdf`
  - Direct PDF: `https://www.uscourts.gov/sites/default/files/ao088b.pdf`
  - Source page: `https://www.uscourts.gov/forms-rules/forms/subpoena-produce-documents-information-or-objects-or-permit-inspection-premises-a-civil-action`
  - Why it is useful: multi-page subpoena with caption blocks, combo-box court selection, and service/production sections

- `legal/texas-15th-court-civil-docketing-statement.pdf`
  - Direct PDF: `https://www.txcourts.gov/media/1459205/cv_docketingstatement_expanded092024.pdf`
  - Source page: `https://www.txcourts.gov/15thcoa/practice-before-the-court/`
  - Why it is useful: very large appellate form with a dense field tree and repeated legal metadata sections

- `legal/illinois-financial-affidavit.pdf`
  - Direct PDF: `https://ilcourtsaudio.blob.core.windows.net/antilles-resources/resources/2cb2c0ce-20f8-4eb5-9d23-05664d7f4404/FA%20Financial%20Affidavit.pdf`
  - Source page: `https://www.illinoiscourts.gov/forms/approved-forms/forms-approved-statewide/divorce-child-support-maintenance/`
  - Why it is useful: large multi-page affidavit with many numeric fields, repeated sections, and signature-heavy structure

- `legal/i589-asylum-application.pdf`
  - Direct PDF: `https://www.uscis.gov/sites/default/files/document/forms/i-589.pdf`
  - Source page: `https://www.uscis.gov/i-589`
  - Why it is useful: another real USCIS XFA form with a large field tree
  - Important note: the new implementation should reject this as XFA even though the old Syncfusion snapshot currently treats it as supported

- `legal/ca-gc110-temp-guardianship-petition.pdf`
  - Direct PDF: `https://courts.ca.gov/documents/gc110.pdf`
  - Source page: `https://selfhelp.courts.ca.gov/jcc-form/GC-110`
  - Why it is useful: California temporary guardianship petition with mixed text, checkbox, and signature-style petitioner blocks

- `legal/ca-gc248-duties-acknowledgment.pdf`
  - Direct PDF: `https://courts.ca.gov/documents/gc248.pdf`
  - Source page: `https://selfhelp.courts.ca.gov/jcc-form/GC-248`
  - Why it is useful: probate guardianship acknowledgment with a mostly boilerplate layout and a small editable acknowledgment area

- `legal/ca-gc210-guardianship-petition-minor.pdf`
  - Direct PDF: `https://courts.ca.gov/documents/gc210.pdf`
  - Source page: `https://selfhelp.courts.ca.gov/jcc-form/GC-210`
  - Why it is useful: real California Judicial Council guardianship form delivered as XFA
  - Important note: the new implementation should reject this as XFA even though the old Syncfusion snapshot currently treats it as supported

- `legal/ca-gc310-conservatorship-petition.pdf`
  - Direct PDF: `https://courts.ca.gov/documents/gc310.pdf`
  - Source page: `https://selfhelp.courts.ca.gov/jcc-form/GC-310`
  - Why it is useful: large California conservatorship petition delivered as XFA with repeated person-and-estate sections
  - Important note: the new implementation should reject this as XFA even though the old Syncfusion snapshot currently treats it as supported

## Insurance forms

- `insurance/cms1490s-patient-request-medical-payment.pdf`
  - Direct PDF: `https://www.cms.gov/medicare/cms-forms/cms-forms/downloads/cms1490s-english.pdf`
  - Source page: `https://www.cms.gov/medicare/cms-forms/cms-forms/cms-forms-items/cms012949`
  - Why it is useful: official Medicare beneficiary claim form with long radio choice labels and mixed read-only fields

- `insurance/cms-40b-part-b-enrollment.pdf`
  - Direct PDF: `https://www.cms.gov/Medicare/CMS-Forms/CMS-Forms/Downloads/CMS40B-E.pdf`
  - Source page: `https://www.cms.gov/medicare/cms-forms/cms-forms/cms40b-application-enrollment-part-b`
  - Why it is useful: official CMS enrollment form with comb-style inputs, JavaScript formatting, and signature-heavy layout

- `insurance/cms-l564-employment-info.pdf`
  - Direct PDF: `https://www.cms.gov/Medicare/CMS-Forms/CMS-Forms/Downloads/CMS-L564E.pdf`
  - Source page: `https://www.cms.gov/cms-l564-request-employment-information`
  - Why it is useful: official CMS employer information form with checkboxes, segmented identifiers, and signature fields

- `insurance/va-5655-financial-status-report.pdf`
  - Direct PDF: `https://www.va.gov/vaforms/va/pdf/VA5655.pdf`
  - Source page: `https://www.va.gov/find-forms/about-form-5655/`
  - Why it is useful: non-USCIS government XFA form with dense financial sections
  - Important note: the new implementation should reject this as XFA even though the old Syncfusion snapshot currently treats it as supported

- `insurance/vha-10-0426-release-of-information.pdf`
  - Direct PDF: `https://www.va.gov/vaforms/medical/pdf/vha-10-0426-fill.pdf`
  - Source page: `https://www.va.gov/find-forms/about-form-10-0426/`
  - Why it is useful: non-USCIS government XFA form with medical authorization semantics
  - Important note: the new implementation should reject this as XFA even though the old Syncfusion snapshot currently treats it as supported

- `insurance/cms1500-health-insurance-claim-form.pdf`
  - Direct PDF: `https://www.cms.gov/medicare/cms-forms/cms-forms/downloads/cms1500.pdf`
  - Source page: `https://www.cms.gov/medicare/cms-forms/cms-forms/cms-forms-items/cms026905`
  - Why it is useful: official claim form packet that the old and new tools both classify as unsupported despite having an AcroForm shell

- `insurance/marketplace-employer-appeal-form.pdf`
  - Direct PDF: `https://www.cms.gov/files/document/marketplace-employer-appeal-form-static.pdf`
  - Source page: `https://www.cms.gov/cciio/resources/forms-reports-and-other-resources/appeals/forms`
  - Why it is useful: real appeal workflow form with checkbox/radio/text mixtures and a clean public government source

- `insurance/indiana-cshcs-request-for-authorization.pdf`
  - Direct PDF: `https://forms.in.gov/Download.aspx?id=12032`
  - Source page: `https://www.in.gov/health/cshcs/prior-authorization/`
  - Why it is useful: state authorization form with many fields and insurance-style approval workflow structure

- `insurance/indiana-workers-comp-physicians-report-form-2118.pdf`
  - Direct PDF: `https://forms.in.gov/Download.aspx?id=8502`
  - Source page: `https://www.in.gov/idoi/wcb/board-forms`
  - Why it is useful: workers-comp medical form with many fields, read-only labels, and push-button widgets
