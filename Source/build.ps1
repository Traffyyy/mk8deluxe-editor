param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\App'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
# Put arguments in a response file so deeply nested source paths and all 131
# embedded images do not exceed Windows' process command-line length limit.
function Invoke-CSharpCompiler([string[]]$CompilerArguments) {
    $responsePath = Join-Path $OutputDirectory ('compiler-' + [Guid]::NewGuid().ToString('N') + '.rsp')
    try {
        $lines = $CompilerArguments | ForEach-Object { '"' + $_ + '"' }
        [System.IO.File]::WriteAllLines($responsePath, [string[]]$lines, [System.Text.Encoding]::UTF8)
        & $compiler ('@' + $responsePath)
        if ($LASTEXITCODE -ne 0) { throw ('C# compilation failed with exit code ' + $LASTEXITCODE) }
    }
    finally {
        if (Test-Path -LiteralPath $responsePath) { Remove-Item -LiteralPath $responsePath -Force }
    }
}
$coreFiles = (Get-ChildItem (Join-Path $PSScriptRoot 'Mk8Editor.Core') -Filter '*.cs').FullName
$appFiles = (Get-ChildItem (Join-Path $PSScriptRoot 'Mk8Editor.App') -Filter '*.cs').FullName
$catalog = Join-Path $PSScriptRoot 'Mk8Editor.Core\catalog.json'
$xaml = Join-Path $PSScriptRoot 'Mk8Editor.App\MainWindow.xaml'
$icon = Join-Path $PSScriptRoot 'Mk8Editor.App\App.ico'
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Runtime.Serialization.dll')
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { Join-Path $framework "WPF\$_" }
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:' + (Join-Path $OutputDirectory 'mk8deluxe editor.exe')))
$arguments += $references | ForEach-Object { '/reference:' + $_ }
$arguments += "/resource:$catalog,Mk8Editor.catalog.json"
$arguments += "/resource:$xaml,Mk8Editor.MainWindow.xaml"
$arguments += "/win32icon:$icon"
$arguments += "/resource:$icon,Mk8Editor.App.ico"
foreach ($image in Get-ChildItem (Join-Path $PSScriptRoot 'Mk8Editor.App\Assets') -Filter '*.png') {
    $arguments += "/resource:$($image.FullName),Mk8Editor.Assets.$($image.Name)"
}
Invoke-CSharpCompiler ($arguments + $coreFiles + $appFiles)
$testFile = Join-Path $PSScriptRoot 'Mk8Editor.Tests\Program.cs'
$testArguments = @('/nologo', '/target:exe', '/reference:System.Runtime.Serialization.dll',
    ('/out:' + (Join-Path $OutputDirectory 'Mk8Editor.Tests.exe')), "/resource:$catalog,Mk8Editor.catalog.json")
Invoke-CSharpCompiler ($testArguments + $coreFiles + @($testFile))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Mk8Editor.App\App.config') -Destination (Join-Path $OutputDirectory 'mk8deluxe editor.exe.config') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDirectory 'Audio') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Mk8Editor.App\Audio\reference.wav') -Destination (Join-Path $OutputDirectory 'Audio\reference.wav') -Force
Write-Output "Created: $(Join-Path $OutputDirectory 'mk8deluxe editor.exe')"
