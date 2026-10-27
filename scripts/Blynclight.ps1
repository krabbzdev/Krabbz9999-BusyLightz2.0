<#
.SYNOPSIS
  Control an Embrava Blynclight over USB (no Embrava account or API key needed).

.EXAMPLE
  .\Blynclight.ps1 -Color red
  .\Blynclight.ps1 -Color green -Dim
  .\Blynclight.ps1 -Color FF8800 -Flash -Speed fast
  .\Blynclight.ps1 -Status busy        # available | busy | away | dnd | oncall
  .\Blynclight.ps1 -Off
  .\Blynclight.ps1 -List

.NOTES
  If Embrava Connect is running it may overwrite the light when your Teams/Zoom
  presence changes. Quit it from the system tray if you want full manual control.
#>
[CmdletBinding(DefaultParameterSetName = 'Color')]
param(
    [Parameter(ParameterSetName = 'Color', Position = 0)]
    [string]$Color = 'white',

    [Parameter(ParameterSetName = 'Status')]
    [ValidateSet('available', 'busy', 'away', 'dnd', 'oncall', 'offline')]
    [string]$Status,

    [Parameter(ParameterSetName = 'Color')]
    [Parameter(ParameterSetName = 'Status')]
    [switch]$Flash,

    [Parameter(ParameterSetName = 'Color')]
    [Parameter(ParameterSetName = 'Status')]
    [ValidateSet('slow', 'medium', 'fast')]
    [string]$Speed = 'medium',

    [Parameter(ParameterSetName = 'Color')]
    [Parameter(ParameterSetName = 'Status')]
    [switch]$Dim,

    [Parameter(ParameterSetName = 'Off')]
    [switch]$Off,

    [Parameter(ParameterSetName = 'List')]
    [switch]$List
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\BlynclightCore.ps1"

$lights = @(Get-Blynclight)

if ($List) {
    if (-not $lights) { Write-Host 'No Blynclight found.' } else { $lights | Format-Table Name, ProductId, InstanceId -AutoSize }
    return
}
if (-not $lights) { throw 'No Blynclight found. Check that it is plugged in.' }

if ($Status) {
    $Color = @{ available = 'green'; busy = 'red'; away = 'yellow'; dnd = 'purple'; oncall = 'red'; offline = '000000' }[$Status]
    if ($Status -eq 'offline') { $Off = $true }
    if ($Status -eq 'oncall' -and -not $PSBoundParameters.ContainsKey('Flash')) { $Flash = $true }
}

$report = if ($Off) { New-Report @(0, 0, 0) $true $false $false 'medium' }
          else { New-Report (ConvertTo-Rgb $Color) $false $Dim.IsPresent $Flash.IsPresent $Speed }

Send-BlynclightReport $lights $report
$desc = if ($Off) { 'off' } else { "$Color$(if ($Dim) { ', dim' })$(if ($Flash) { ", flashing ($Speed)" })" }
Write-Host "Blynclight set to $desc."
