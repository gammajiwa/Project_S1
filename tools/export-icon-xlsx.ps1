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
$rows = @((Get-Content $csv | Select-Object -Skip 1) | ConvertFrom-Csv)
if (-not $rows.Count) { throw 'CSV kosong.' }

$headers = 'Ikon', 'Bintang', 'Petak', 'Nama', 'Id', 'Layer', 'Elemen', 'Bentuk',
           'File', 'Status', 'Selesai', 'Kind', 'Deskripsi', 'Prompt'

$nCol  = $headers.Count
$first = 2
$last  = $rows.Count + 1

# Excel refuses calls while it is busy (RPC_E_CALL_REJECTED) and .NET turns that into a hard
# exception. The supported answer is a message filter: it tells COM to retry instead of throwing.
if (-not ('ComRetryFilter' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

[ComImport, Guid("00000016-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig] int HandleInComingCall(int callType, IntPtr taskCaller, int tickCount, IntPtr interfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr taskCallee, int tickCount, int rejectType);
    [PreserveSig] int MessagePending(IntPtr taskCallee, int tickCount, int pendingType);
}

public class ComRetryFilter : IOleMessageFilter
{
    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);

    public static void Register() { IOleMessageFilter old; CoRegisterMessageFilter(new ComRetryFilter(), out old); }
    public static void Revoke()   { IOleMessageFilter old; CoRegisterMessageFilter(null, out old); }

    int IOleMessageFilter.HandleInComingCall(int callType, IntPtr taskCaller, int tickCount, IntPtr interfaceInfo)
    {
        return 0;   // SERVERCALL_ISHANDLED
    }

    int IOleMessageFilter.RetryRejectedCall(IntPtr taskCallee, int tickCount, int rejectType)
    {
        // SERVERCALL_RETRYLATER: wait 250 ms and try the same call again, forever.
        return rejectType == 2 ? 250 : -1;
    }

    int IOleMessageFilter.MessagePending(IntPtr taskCallee, int tickCount, int pendingType)
    {
        return 2;   // PENDINGMSG_WAITDEFPROCESS
    }
}
'@
}
[ComRetryFilter]::Register()

$excel = New-Object -ComObject Excel.Application

# A freshly launched Excel — or one recovering from a killed instance — rejects calls with
# RPC_E_CALL_REJECTED until it finishes starting. Wait for it rather than failing the run.
$ready = $false
for ($try = 1; $try -le 15; $try++) {
    try { if ($excel.Workbooks) { $ready = $true; break } } catch { }
    Start-Sleep -Milliseconds 700
}
if (-not $ready) {
    try { $excel.Quit() } catch { }
    throw 'Excel tidak siap menerima perintah. Tutup semua jendela Excel lalu ulangi.'
}

$excel.Visible = $false
$excel.DisplayAlerts = $false
$excel.AutomationSecurity = 3   # msoAutomationSecurityForceDisable

$failure = $null
try {
    $book  = $excel.Workbooks.Add()
    while ($book.Worksheets.Count -gt 1) { $book.Worksheets.Item($book.Worksheets.Count).Delete() }

    $sheet = $book.Worksheets.Item(1)
    $sheet.Name = 'Ikon'

    # --- write everything in two block assignments, not 1400 COM round trips ---
    $head = New-Object 'object[,]' 1, $nCol
    for ($c = 0; $c -lt $nCol; $c++) { $head[0, $c] = $headers[$c] }
    $sheet.Range($sheet.Cells.Item(1, 1), $sheet.Cells.Item(1, $nCol)).Value2 = $head

    $grid = New-Object 'object[,]' $rows.Count, $nCol
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $row = $rows[$i]
        # column 1 (Ikon) and 11 (Selesai) stay empty on purpose: one holds a picture, one is yours to tick
        $grid[$i, 1]  = [int]$row.Bintang
        $grid[$i, 2]  = [int]$row.Petak
        $grid[$i, 3]  = $row.Nama
        $grid[$i, 4]  = $row.Id
        $grid[$i, 5]  = $row.Layer
        $grid[$i, 6]  = $row.Elemen
        $grid[$i, 7]  = $row.Bentuk
        $grid[$i, 8]  = $row.FileIkon
        $grid[$i, 9]  = $row.Status
        $grid[$i, 11] = $row.Kind
        $grid[$i, 12] = $row.Deskripsi
        $grid[$i, 13] = $row.Prompt
    }
    $sheet.Range($sheet.Cells.Item($first, 1), $sheet.Cells.Item($last, $nCol)).Value2 = $grid

    # --- geometry BEFORE pictures: a shape is anchored by absolute points, so inserting one and
    #     then widening its column leaves it floating over the wrong cell ---
    $widths = @{ 1 = 7; 2 = 8; 3 = 6; 4 = 22; 5 = 18; 6 = 8; 7 = 10; 8 = 9; 9 = 24; 10 = 12; 11 = 8; 12 = 13; 13 = 46; 14 = 70 }
    foreach ($k in $widths.Keys) { $sheet.Columns.Item($k).ColumnWidth = $widths[$k] }
    $sheet.Range($sheet.Cells.Item($first, 1), $sheet.Cells.Item($last, $nCol)).RowHeight = 40

    # Only the rows in use. Wrapping a whole column asks Excel to lay out a million rows, which
    # sends it busy long enough to reject the next call outright.
    foreach ($k in 13, 14) {
        $col = $sheet.Range($sheet.Cells.Item($first, $k), $sheet.Cells.Item($last, $k))
        $col.WrapText = $true
        $col.VerticalAlignment = -4160   # xlTop
    }

    $placed = 0
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $path = $rows[$i].PathIkon
        if (-not $path -or -not (Test-Path $path)) { continue }
        $anchor = $sheet.Cells.Item($first + $i, 1)
        # 0 = msoFalse (do not link), -1 = msoCTrue (save the image inside the workbook)
        [void]$sheet.Shapes.AddPicture($path, 0, -1, $anchor.Left + 5, $anchor.Top + 3, 34, 34)
        $placed++
    }

    # A literal suffix, not a repeat count: "3★" sorts and filters as the number it still is.
    $sheet.Range("B$first`:B$last").NumberFormat = '0"★"'
    $sheet.Range("B$first`:C$last").HorizontalAlignment = -4108   # xlCenter

    $headRange = $sheet.Range($sheet.Cells.Item(1, 1), $sheet.Cells.Item(1, $nCol))
    $headRange.Font.Bold = $true
    $headRange.Interior.Color = 0x14202A     # BGR, so this is a dark brown
    $headRange.Font.Color = 0xB0D9E8
    $headRange.HorizontalAlignment = -4131   # xlLeft
    $sheet.Rows.Item(1).RowHeight = 22

    # Freeze the header and filter on it: this sheet exists to be re-sorted per batch.
    $sheet.Activate()
    $win = $book.Windows.Item(1)
    $win.SplitRow = 1
    $win.FreezePanes = $true
    [void]$sheet.Range($sheet.Cells.Item(1, 1), $sheet.Cells.Item($last, $nCol)).AutoFilter()
    [void]$sheet.Cells.Item($first, 4).Select()

    # --- second sheet: the counts, so the size of the job is visible without pivoting ---
    $sum = $book.Worksheets.Add()
    $sum.Name = 'Ringkasan'

    $line = 1
    function Add-Block($sheetRef, $title, $groups, [ref]$at) {
        $sheetRef.Cells.Item($at.Value, 1).Value2 = $title
        $sheetRef.Cells.Item($at.Value, 2).Value2 = 'Jumlah'
        $sheetRef.Range("A$($at.Value):B$($at.Value)").Font.Bold = $true
        $at.Value++
        foreach ($g in $groups) {
            $sheetRef.Cells.Item($at.Value, 1).Value2 = $g.Label
            $sheetRef.Cells.Item($at.Value, 2).Value2 = $g.Count
            $at.Value++
        }
        $at.Value++
    }

    Add-Block $sum 'Bintang' (
        $rows | Group-Object Bintang | Sort-Object { [int]$_.Name } |
            ForEach-Object { [PSCustomObject]@{ Label = "$($_.Name) bintang"; Count = $_.Count } }
    ) ([ref]$line)

    Add-Block $sum 'Layer' (
        $rows | Group-Object Layer | Sort-Object Count -Descending |
            ForEach-Object { [PSCustomObject]@{ Label = $_.Name; Count = $_.Count } }
    ) ([ref]$line)

    Add-Block $sum 'Elemen' (
        $rows | Group-Object Elemen | Sort-Object Count -Descending |
            ForEach-Object { [PSCustomObject]@{ Label = $_.Name; Count = $_.Count } }
    ) ([ref]$line)

    Add-Block $sum 'Jumlah petak' (
        $rows | Group-Object Petak | Sort-Object { [int]$_.Name } |
            ForEach-Object { [PSCustomObject]@{ Label = "$($_.Name) petak"; Count = $_.Count } }
    ) ([ref]$line)

    $sum.Columns.Item(1).ColumnWidth = 18
    $sum.Columns.Item(2).ColumnWidth = 10
    $sum.Move($sheet)   # counts first, worklist second

    if (Test-Path $xlsx) { Remove-Item $xlsx -Force }
    $book.SaveAs($xlsx, 51)   # 51 = xlOpenXMLWorkbook
    $book.Close($false)

    Write-Host "$($rows.Count) baris, $placed ikon tertanam -> $xlsx"
}
catch {
    $failure = $_
}
finally {
    # Quit can itself throw if Excel is mid-operation; that must not hide the real error.
    try { $excel.Quit() } catch { }
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [ComRetryFilter]::Revoke()
}

if ($failure) { throw $failure }
