param([Parameter(Mandatory=$true)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
$request = [IO.File]::ReadAllText($RequestPath) | ConvertFrom-Json
$null = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime]
$null = [Windows.UI.Notifications.NotificationSetting, Windows.UI.Notifications, ContentType=WindowsRuntime]
$null = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime]
$document = New-Object System.Xml.XmlDocument
$toast = $document.CreateElement('toast'); $document.AppendChild($toast) | Out-Null
$toast.SetAttribute('activationType','foreground'); $toast.SetAttribute('launch','windrop://show')
$visual = $document.CreateElement('visual'); $toast.AppendChild($visual) | Out-Null
$binding = $document.CreateElement('binding'); $binding.SetAttribute('template','ToastGeneric'); $visual.AppendChild($binding) | Out-Null
foreach ($line in @($request.title, $request.body)) { $text = $document.CreateElement('text'); $text.InnerText = [string]$line; $binding.AppendChild($text) | Out-Null }
if ($request.image -and (Test-Path -LiteralPath $request.image)) {
    $image = $document.CreateElement('image'); $image.SetAttribute('placement','hero'); $image.SetAttribute('src',([Uri]$request.image).AbsoluteUri); $binding.AppendChild($image) | Out-Null
}
if ($request.id) {
    $actions = $document.CreateElement('actions'); $toast.AppendChild($actions) | Out-Null
    foreach ($item in @(@{ Verb='Accept'; Label=[string]$request.accept },@{ Verb='Decline'; Label=[string]$request.decline })) {
        $label = if ($item.Label) { $item.Label } else { $item.Verb }
        $action = $document.CreateElement('action'); $action.SetAttribute('content',$label); $action.SetAttribute('activationType','foreground'); $action.SetAttribute('arguments',('windrop://' + $item.Verb.ToLowerInvariant() + '?id=' + $request.id)); $actions.AppendChild($action) | Out-Null
    }
}
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument; $xml.LoadXml($document.OuterXml)
$notification = [Windows.UI.Notifications.ToastNotification]::new($xml)
$notification.Tag = [string]$request.tag; $notification.Group = 'SwooshDrop'
$notification.ExpirationTime = [DateTimeOffset]::Now.AddSeconds(55)
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('WinDrop.PC')
$setting = $notifier.Setting
if ($null -ne $setting -and [int]$setting -ne 0) { throw 'Windows notifications are disabled for SwooshDrop.' }
$notifier.Show($notification)
