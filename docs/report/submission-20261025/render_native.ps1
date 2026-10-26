$ErrorActionPreference = 'Stop'
$work = $PSScriptRoot
$source = Join-Path $work '阅记-项目报告.docx'
$pdf = Join-Path $work '阅记-项目报告.pdf'
$wordApp = $null
$document = $null
try {
    $wordApp = New-Object -ComObject Word.Application
    $wordApp.Visible = $false
    $wordApp.DisplayAlerts = 0
    $wordApp.AutomationSecurity = 3
    $document = $wordApp.Documents.Open($source, $false, $false, $false, '', '', $false, '', '', 0, 0, $false, $false, 0, $true)
    $document.Repaginate()
    $null = $document.Fields.Update()
    foreach ($toc in $document.TablesOfContents) {
        $toc.TabLeader = 1
        $toc.RightAlignPageNumbers = $true
        $toc.IncludePageNumbers = $true
        $toc.Update()
        $contentWidth = $document.PageSetup.PageWidth - $document.PageSetup.LeftMargin - $document.PageSetup.RightMargin
        foreach ($paragraph in $toc.Range.Paragraphs) {
            $format = $paragraph.Format
            $format.CharacterUnitLeftIndent = 0
            $format.CharacterUnitRightIndent = 0
            $format.CharacterUnitFirstLineIndent = 0
            $isChild = $paragraph.Range.Text -match '^[0-9]+[.][0-9]+[ ]'
            $format.LeftIndent = $(if ($isChild) {22} else {0})
            $format.FirstLineIndent = 0
            $format.RightIndent = 0
            $format.SpaceBefore = 0
            $format.SpaceAfter = 5
            $format.KeepWithNext = 0
            $format.TabStops.ClearAll()
            $null = $format.TabStops.Add($contentWidth, 2, 1)
        }
    }
    $document.Repaginate()
    foreach ($toc in $document.TablesOfContents) { $toc.UpdatePageNumbers() }
    # Keep native Word pagination as final authority; field styles remain editable.
    $document.Save()
    $document.ExportAsFixedFormat($pdf,17,$false,0,0,1,99999,0,$true,$true,1,$true,$true,$false)
    $headings = @()
    foreach ($paragraph in $document.Paragraphs) {
        if ($paragraph.Style.NameLocal -in @('标题 11','标题 21')) {
            $headings += @{ title=$paragraph.Range.Text.Trim(); level=$paragraph.OutlineLevel; page=$paragraph.Range.Information(3) }
        }
    }
    $tocText = @($document.TablesOfContents | ForEach-Object { $_.Range.Text })
    $result = @{renderer='Microsoft Word native COM';wordVersion=$wordApp.Version;pages=$document.ComputeStatistics(2);tocCount=$document.TablesOfContents.Count;headings=$headings;tocText=$tocText;pdf=$pdf}
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $work 'native-render.json') -Encoding utf8
    $result | Select-Object renderer,pages,tocCount | ConvertTo-Json
} finally {
    if ($null -ne $document) { $document.Close(0); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($document) }
    if ($null -ne $wordApp) { $wordApp.Quit(); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordApp) }
}
