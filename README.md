# nav-tttool
A tool we use at [Singhammer IT Consulting](https://www.singhammer.com/) to export and import Tooltips from NAV Objects. It's main purpose is to help with translation of tooltips to additional languages. 

## General
This small utility reads [Microsoft Dynamics NAV](https://www.microsoft.com/en-us/dynamics365/nav-overview) objects and exports the contained tooltips into a tab-separated (tsv) file. Tooltips can be imported back from the tsv file into the same text file(s) where they have been initially extracted from. Optionally, it can generate entries for Controls and Actions that do not yet have a tooltip set.

The tsv file can be processed in tools like [LibreOffice Calc](https://www.libreoffice.org/discover/calc/) or [Google Sheets](https://docs.google.com/spreadsheets) and be sent to external translators or sorted and modified in every way. I had troubles exporting from [Microsoft Excel](https://products.office.com/excel) back to the correct tsv format again (i.e. no column headers, no quotes around columns), but maybe that is just me.

## Usage
The tool can either run in export or import mode. Exporting means reading one or more NAV objects in text format into a tsv file, import the opposite.

### Export
`Tooltip.exe directory\*.txt -export filename.tsv [-generateTooltips]`

* Parameter 1 (* directory\\*.txt): Specifies a path and file pattern of text files that should be read
* Parameter 2 (-export filename.tsv): Specifies export mode and what file to write the tooltips to
* Parameter 3 (-generateTooltips): Optional. If no tooltip is present, the export will try to come up with something meaningful for later manual processing (see below)

### Import
`Tooltip.exe directory -import filename.tsv`

* Parameter 1 (directory): Specifies a path where the text files to read the tooltips into can be found
* Parameter 2 (-import filename.tsv): Specifies import mode and what file to read the tooltips from

*Note: Each line in the tsv file contains a reference to the file the tooltip was originally read from. During import, the tool will try to read the tooltip back into the same file. So you have to keep the file structure intact between exporting and importing.*

*Note: Text files can contain one or multiple objects. So you can have either one file per object or multiple objects in one text file.

## Generated tooltips

Since it can be hard to tell which actions and fields are missing tooltips, the tool will optionally generate specially marked entries in the exported file (see `-generateTooltips` parameter). It tries to come up with a meaningful tooltip suggestion by exporting the following properties for every control that has no tooltip:

1. If the control has a caption, use that as suggestion. Exported entries will be prepended by three hashtags (###) to clarify that this entry comes from a caption.
1. Otherwise, if the control has a source expression, use that as suggestion. Exported entries will be prepended by three hashtags (###) and have their language code set to @@@ to clarify that this entry comes from a source expression.

## Example

1. Export a couple of pages from the C/SIDE client to c:\temp\pages.txt
1. Export the tooltips
`Tooltip.exe "c:\temp\pages.txt" -export "c:\temp\tooltips.tsv" -generateTooltips`
1. Modify toolstips.csv and import the file back: 
`Tooltip.exe "c:\temp" -import "c:\temp\tooltips.tsv"`

## File format

The tsv will look like this (example extracted from Page 4, the headers are included here just for documentation purposes):

Filename | Object Type | Object Number | Control Type (number) | Control Type (text) | Control id | Language 1 Code | Language 1 Tooltip | Duplicate | Language 2 Code | Language 2 Tooltip | Duplicate | ...
------ | ------- |------ | ------- |------ | ------- | ------ | ------- | ------- | ------- | ------- | ------- | ------- |
pages.txt|21|4|29|Control|2|DEU|Gibt einen Code zur Identifizierung...|0|ENU|Specifies a code to identify...|0|...
pages.txt|21|4|29|Control|4|DEU|Gibt eine Formel zur Berechnung...|0|ENU|Specifies a formula that...|0|...
pages.txt|21|4|29|Control|6|DEU|Zeigt die Datumsformel an, ...|0|ENU|Specifies the date formula if...|0|...

Note: The *duplicate* column helps finding tooltips that exist multiple times. The first occurence of a language code + text combination is marked by a 0. Every repetition of that combination gets a 1. So you can avoid translating the same text over and over again by paying attention to this column.

## Notes on compiling from source code

* Should compile with Visual Studio 2015 or similar
* Several Dynamics NAV DLLs must be copied from the "RoleTailored Client" folder into the folder `lib/2017`
  * Microsoft.Dynamics.Nav.Model.dll
  * Microsoft.Dynamics.Nav.Model.Parser.dll
  * Microsoft.Dynamics.Nav.Model.Tools.dll
  * Microsoft.Dynamics.Nav.Model.TypeSystem.dll

## Binary release

If you do not want to compile the source code but run the tool anyway, you are free to download the releases provided in this repository. Please do note that I am not distributing the DLLs that this tool depends on. You can copy them from your Dynamics NAV installation - see the included Readme.txt for details.
