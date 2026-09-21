Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NL {
  [DllImport("kernel32", SetLastError=true, CharSet=CharSet.Unicode)]
  public static extern IntPtr LoadLibrary(string p);
}
"@
$native = "E:\WorkSpace\SWD\NCLCCT\sunwoda2.0.0.1\SUNWODA_SEVB\bin\Debug\net8.0-windows\runtimes\win-x64\native"
function TryLoad([string]$name) {
  $h = [NL]::LoadLibrary((Join-Path $native $name))
  $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
  Write-Output "$name => handle=$h err=$err"
}
TryLoad "cudart64_12.dll"
TryLoad "cublas64_12.dll"
TryLoad "cudnn64_9.dll"
TryLoad "onnxruntime_providers_shared.dll"
TryLoad "onnxruntime.dll"
TryLoad "onnxruntime_providers_cuda.dll"
