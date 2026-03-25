# Setting up Google Sheets Export

## Create a Google Cloud Project

1. Go to https://console.cloud.google.com/ and accept the terms of service
2. Click the project dropdown in the top left corner → **New Project**
3. Enter a name, e.g. `PracticeSessionExporter` → **Create**

## Enable the Google Sheets API

> Make sure your project is selected

1. Open the navigation menu (top left) → **APIs & Services** → **Library**
2. Search for **Google Sheets API** → Click on it → **Enable**

## Create a Service Account

1. Open the navigation menu (top left) → **APIs & Services**
2. Go to **Credentials** → **Create Credentials** → **Service Account**
3. Enter a name, e.g. `PracticeSessionExporter` → **Create and Continue**
4. Skip the Permissions and Access sections → **Done**

## Download your credentials

1. Click on your newly created Service Account
2. Go to **Keys** → **Add Key** → **Create New Key** → **JSON** → **Create**
3. A JSON file will be downloaded automatically

## Give the Service Account access to your sheet

1. Open the downloaded JSON file, find the `client_email` field and copy the value
   - e.g. `practicesessionexporter@your-project-name.iam.gserviceaccount.com`
2. Open your sheet → **Share** (top right)
3. Paste the `client_email` address, set the role to **Editor** → **Send**

## Configure Speebrun Consistency Tracker

1. Rename the downloaded file to `credentials.json`
2. Navigate to your Celeste base folder (Open Olympus → Manage → Browse)
3. Create a folder named `SCT_Exports` and place `credentials.json` inside it
4. In the same folder, create a file named `settings.json`
5. Find your Spreadsheet ID in the sheet's URL, between `.../spreadsheets/d/` and `/edit`
   - e.g. `1-n_znwRIokx32TMMwnW_0wiHUIExgZF1pEYu8VUhXBM` for Astro's template
6. Paste the following into `settings.json`, replacing `YOUR_SHEET_ID` with your actual ID and adjusting `TabName` and `StartCell` as needed:

```json
{
    "SpreadsheetId": "YOUR_SHEET_ID",
    "TabName": "Session Export",
    "StartCell": "A1"
}
```

> The tab named in `TabName` will be created automatically if it doesn't exist. `StartCell` is optional — it controls which cell the export starts from.
