# Data Parser Usage

Recommended Python version: 3.11.

## Windows Setup

Run these commands from the repository root:

```powershell
py -3.11 -m venv .venv-parser
.venv-parser\Scripts\activate
python -m pip install --upgrade pip setuptools wheel
pip install -r data-parser/requirements.txt
```

Set the Anthropic API key for the current PowerShell session:

```powershell
$env:ANTHROPIC_API_KEY="sk-ant-..."
```

## Example Runs

Extract-only run without Claude:

```powershell
python data-parser/parser_niche_and_categories_v2.py --input sites.xlsx --output extraction_debug.xlsx --max-sites 20 --extract-only --selenium-mode auto
```

Small normal test run:

```powershell
python data-parser/parser_niche_and_categories_v2.py --input sites.xlsx --output sites_with_categories_v2.xlsx --max-sites 20 --selenium-mode auto
```

## Selenium Fallback

Selenium is an optional fallback for pages where regular HTTP fetching fails or returns weak text.

Use `--selenium-mode` to control it:

- `auto`: use Selenium if dependencies are available. If Selenium is unavailable, the parser logs one startup warning and continues HTTP-only.
- `off`: never use Selenium fallback. This is HTTP-only mode.
- `required`: fail fast at startup if Selenium dependencies are unavailable.

On Python 3.12, some Selenium dependency chains can fail because `distutils` was removed from Python. If that happens, prefer Python 3.11 for this parser, or run:

```powershell
python -m pip install --upgrade setuptools wheel
```
