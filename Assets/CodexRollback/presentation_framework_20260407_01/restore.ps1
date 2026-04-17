$backupRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$map = @{
    'PublicActionQueueUI.cs.bak.txt' = 'D:\Program Files\Unity\Item\AssasBin\Assets\Scripts\Action\PublicActionQueueUI.cs'
    'MatchManager.cs.bak.txt' = 'D:\Program Files\Unity\Item\AssasBin\Assets\Scripts\Rules\MatchManager.cs'
    'PlayerState.cs.bak.txt' = 'D:\Program Files\Unity\Item\AssasBin\Assets\Scripts\Rules\PlayerState.cs'
    'SelectionUI.cs.bak.txt' = 'D:\Program Files\Unity\Item\AssasBin\Assets\Scripts\LocalShow\CardPreview\SelectionUI.cs'
}
foreach ($entry in $map.GetEnumerator()) {
    Copy-Item (Join-Path $backupRoot $entry.Key) $entry.Value -Force
}
Write-Host 'Restore completed.'
