# Turns icon-worklist.csv into a real .xlsx with the current placeholder embedded on every row.
#
# The CSV stays the source of truth; this only presents it. Run export-icon-worklist.ps1 first.
# Requires Excel to be installed (COM automation).
#
# Safe to re-run: it overwrites the workbook.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$art  = Join-Path $root 'design\art'
$csv  = Join-Path $art 'icon-worklist.csv'
$xlsx = Join-Path $art 'icon-worklist.xlsx'

if (-not (Test-Path $csv)) { throw "Jalankan export-icon-worklist.ps1 dulu: $csv belum ada." }

# Skip the leading "sep=," line, which exists purely so Excel parses the CSV on any locale.
$rows = (Get-Content $csv | Select-Object -Skip 1) | ConvertFrom-Csv
if (-not $rows) { throw 'CSV kosong.' }

$headers = 'Ikon', 'Bintang', 'Petak', 'Nama', 'Id', 'Layer', 'Elemen', 'Bentuk',
           'File', 'Status', 'Selesai', 'Kind', 'Deskripsi', 'Prompt'

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$excel.ScreenUpdating = $false

try {
    $book  = $excel.Workbooks.Add()
    $sheet = $book.Worksheets.Item(1)
    $sheet.Name = 'Ikon'

    for ($c = 0; $c -lt $headers.Count; $c++) {
        $cell = $sheet.Cells.Item(1, $c + 1)
        $cell.Value2 = $headers[$c]
        $cell.Font.Bold = $true
    }

    $head = $sheet.Range($sheet.Cells.Item(1, 1), $sheet.Cells.Item(1, $headers.Count))
    $head.Interior.Color = 0x2A1F14
    $head.Font.Color = 0xB0D9E8
    $head.HorizontalAlignment = -4131

    $r = 2
    foreach ($row in $rows) {
        $sheet.Cells.Item($r, 2).Value2  = [int]$row.Bintang
        $sheet.Cells.Item($r, 3).Value2  = [int]$row.Petak
        $sheet.Cells.Item($r, 4).Value2  = $row.Nama
        $sheet.Cells.Item($r, 5).Value2  = $row.Id
        $sheet.Cells.Item($r, 6).Value2  = $row.Layer
        $sheet.Cells.Item($r, 7).Value2  = $row.Elemen
        $sheet.Cells.Item($r, 8).Value2  = $row.Bentuk
        $sheet.Cells.Item($r, 9).Value2  = $row.FileIkon
        $sheet.Cells.Item($r, 10).Value2 = $row.Status
        $sheet.Cells.Item($r, 12).Value2 = $row.Kind
        $sheet.Cells.Item($r, 13).Value2 = $row.Deskripsi
        $sheet.Cells.Item($r, 14).Value2 = $row.Prompt

        $sheet.Rows.Item($r).RowHeight = 40

        if ($row.PathIkon -and (Test-Path $row.PathIkon)) {
            $anchor = $sheet.Cells.Item($r, 1)
            # msoFalse link-to-file, msoCTrue save-with-document, then explicit points.
            [void]$sheet.Shapes.AddPicture($row.PathIkon, 0, -1,
                $anchor.Left + 4, $anchor.Top + 3, 34, 34)
        }

        $r++
    }

    $last = $r - 1

    # Star column reads as stars, not as a bare number, without losing sortability.
    $sheet.Range("B2:B$last").NumberFormat = '"★"*0'
    $sheet.Range("B2:B$last").HorizontalAlignment = -4108
    $sheet.Range("C2:C$last").HorizontalAlignment = -4108

    $widths = @{ 1 = 7; 2 = 8; 3 = 6; 4 = 22; 5 = 18; 6 = 8; 7 = 10; 8 = 9; 9 = 24; 10 = 12; 11 = 8; 12 = 13; 13 = 46; 14 = 70 }
    foreach ($k in $widths.Keys) { $sheet.Columns.Item($k).ColumnWidth = $widths[$k] }

    foreach ($k in 13, 14) {
        $sheet.Columns.Item($k).WrapText = $true
        $sheet.Columns.Item($k).VerticalAlignment = -4160
    }

    # Freeze the header, and filter on it: this sheet exists to be re-sorted per batch.
    $sheet.Activate()
    $excel.ActiveWindow.SplitRow = 1
    $excel.ActiveWindow.FreezePanes = $true
    [void]$sheet.Range($sheet.Cells.Item(1, 1), $sheet.Cells.Item($last, $headers.Count)).AutoFilter()

    # --- second sheet: the counts, so the size of the job is visible without pivoting ---
    $sum = $book.Worksheets.Add([System.Reflection.Missing]::Value, $sheet)
    $sum.Name = 'Ringkasan'

    $sum.Cells.Item(1, 1).Value2 = 'Bintang'
    $sum.Cells.Item(1, 2).Value2 = 'Jumlah'
    $sum.Range('A1:B1').Font.Bold = $true

    $i = 2
    foreach ($g in ($rows | Group-Object Bintang | Sort-Object { [int]$_.Name })) {
        $sum.Cells.Item($i, 1).Value2 = "$($g.Name) bintang"
        $sum.Cells.Item($i, 2).Value2 = $g.Count
        $i++
    }

    $i++
    $sum.Cells.Item($i, 1).Value2 = 'Layer'
    $sum.Cells.Item($i, 2).Value2 = 'Jumlah'
    $sum.Range("A$i`:B$i").Font.Bold = $true
    $i++
    foreach ($g in ($rows | Group-Object Layer | Sort-Object Count -Descending)) {
        $sum.Cells.Item($i, 1).Value2 = $g.Name
        $sum.Cells.Item($i, 2).Value2 = $g.Count
        $i++
    }

    $i++
    $sum.Cells.Item($i, 1).Value2 = 'Elemen'
    $sum.Cells.Item($i, 2).Value2 = 'Jumlah'
    $sum.Range("A$i`:B$i").Font.Bold = $true
    $i++
    foreach ($g in ($rows | Group-Object Elemen | Sort-Object Count -Descending)) {
        $sum.Cells.Item($i, 1).Value2 = $g.Name
        $sum.Cells.Item($i, 2).Value2 = $g.Count
        $i++
    }

    $sum.Columns.Item(1).ColumnWidth = 18
    $sum.Columns.Item(2).ColumnWidth = 10

    if (Test-Path $xlsx) { Remove-Item $xlsx -Force }
    $book.SaveAs($xlsx, 51)   # 51 = xlOpenXMLWorkbook (.xlsx)
    $book.Close($false)

    Write-Host "$($rows.Count) baris -> $xlsx"
}
finally {
    $excel.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    [GC]::Collect()
}
