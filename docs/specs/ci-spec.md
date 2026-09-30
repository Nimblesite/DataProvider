# CI and Local Verification

## Local ClinicalCoding Testing [CI-CLINICAL-LOCAL]

The healthcare reference app lives in its own repository,
[Nimblesite/ClinicalCoding](https://github.com/Nimblesite/ClinicalCoding). No
sample or clinical code lives in this repository. To prove a DataProvider change
does not regress it, clone ClinicalCoding next to this checkout and run
`make clinical`.

`tools/clinical-local.sh` packs every package and dotnet tool from this checkout
into `artifacts/local-feed` with a unique `0.0.0-local.<timestamp>` version, then
runs ClinicalCoding's make targets (default `lint test build`) with that version
and feed. The ClinicalCoding working tree is restored on exit.
