- this is primarily about documentaiton and  
- Keep the website tight and simple to navigate. 
- No duplicate pages
- leverage the readmes for content
- Explain what each element of DataProvider does: LQL, DataProvider, Migrations, etc

## DocFX Example Decoding [WEB-DOCFX-EXAMPLES]

Decode DocFX code examples exactly once with the `entities` HTML decoder.
Nested escaped quotes and apostrophes remain entity text after one pass;
ordinary quotes, brackets, apostrophes and ampersands decode normally.
`Website/scripts/test-api-docs.py` runs the real generator against isolated
DocFX fixtures and checks the generated code blocks. Both `make test` and CI
run this regression without changing the real documentation output.
