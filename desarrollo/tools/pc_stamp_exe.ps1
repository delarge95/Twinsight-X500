# Estampa Company / Product / Version en los recursos de version del .exe de PC (PLAN_PC §36).
# Unity 6000.6 no escribe CompanyName/ProductName en TwinSightX500.exe (quedan vacios y ProductVersion = version del motor),
# asi que se sustituye el recurso RT_VERSION con la API de Windows UpdateResource (sin descargar herramientas).
# Uso: powershell -File pc_stamp_exe.ps1 -Exe <ruta\TwinSightX500.exe> [-Version 0.0.3] [-Company TwinSight] [-Product "TwinSight X500"]
param(
  [Parameter(Mandatory=$true)][string]$Exe,
  [string]$Version = "0.0.3",
  [string]$Company = "TwinSight",
  [string]$Product = "TwinSight X500",
  [string]$Copyright = "Copyright (c) 2026 TwinSight"
)
$ErrorActionPreference = "Stop"
$Exe = (Resolve-Path $Exe).Path
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class VerStamp {
  [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern IntPtr BeginUpdateResource(string f, bool del);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool UpdateResource(IntPtr h, IntPtr type, IntPtr name, ushort lang, byte[] data, uint cb);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool EndUpdateResource(IntPtr h, bool discard);
  [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern IntPtr LoadLibraryEx(string f, IntPtr h, uint flags);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool FreeLibrary(IntPtr h);
  delegate bool EnumLangProc(IntPtr m, IntPtr type, IntPtr name, ushort lang, IntPtr p);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool EnumResourceLanguages(IntPtr m, IntPtr type, IntPtr name, EnumLangProc cb, IntPtr p);

  static void Align(MemoryStream ms) { while (ms.Position % 4 != 0) ms.WriteByte(0); }
  // Nodo de version: wLength, wValueLength, wType, szKey, pad, valor, hijos
  static byte[] Node(string key, ushort wType, byte[] value, ushort valueLen, List<byte[]> kids) {
    var ms = new MemoryStream();
    var w = new BinaryWriter(ms);
    w.Write((ushort)0); w.Write(valueLen); w.Write(wType);
    w.Write(Encoding.Unicode.GetBytes(key)); w.Write((ushort)0);
    Align(ms);
    if (value != null) w.Write(value);
    if (kids != null) foreach (var k in kids) { Align(ms); w.Write(k); }
    var arr = ms.ToArray();
    BitConverter.GetBytes((ushort)arr.Length).CopyTo(arr, 0);
    return arr;
  }
  static byte[] Str(string k, string v) {
    byte[] val = Encoding.Unicode.GetBytes(v + "\0");
    return Node(k, 1, val, (ushort)(v.Length + 1), null);
  }
  public static byte[] Build(string version, string company, string product, string copyright, string exeName) {
    var p = version.Split('.'); ushort a = 0, b = 0, c = 0, d = 0;
    if (p.Length > 0) ushort.TryParse(p[0], out a); if (p.Length > 1) ushort.TryParse(p[1], out b);
    if (p.Length > 2) ushort.TryParse(p[2], out c); if (p.Length > 3) ushort.TryParse(p[3], out d);
    uint ms32 = ((uint)a << 16) | b, ls32 = ((uint)c << 16) | d;
    var fx = new MemoryStream(); var fw = new BinaryWriter(fx);
    fw.Write(0xFEEF04BDu); fw.Write(0x00010000u);
    fw.Write(ms32); fw.Write(ls32); fw.Write(ms32); fw.Write(ls32);
    fw.Write(0x3Fu); fw.Write(0u); fw.Write(0x00040004u); fw.Write(1u); fw.Write(0u); fw.Write(0u); fw.Write(0u);
    var strs = new List<byte[]> {
      Str("CompanyName", company), Str("FileDescription", product), Str("FileVersion", version + (p.Length < 4 ? ".0" : "")),
      Str("InternalName", exeName), Str("LegalCopyright", copyright), Str("OriginalFilename", exeName + ".exe"),
      Str("ProductName", product), Str("ProductVersion", version) };
    var table = Node("040904B0", 1, null, 0, strs);
    var sfi = Node("StringFileInfo", 1, null, 0, new List<byte[]> { table });
    var tr = new MemoryStream(); var tw = new BinaryWriter(tr); tw.Write((ushort)0x0409); tw.Write((ushort)0x04B0);
    var vr = Node("Translation", 0, tr.ToArray(), 4, null);
    var vfi = Node("VarFileInfo", 1, null, 0, new List<byte[]> { vr });
    return Node("VS_VERSION_INFO", 0, fx.ToArray(), 52, new List<byte[]> { sfi, vfi });
  }
  public static ushort FindLang(string exe) {
    IntPtr m = LoadLibraryEx(exe, IntPtr.Zero, 0x2); // LOAD_LIBRARY_AS_DATAFILE
    if (m == IntPtr.Zero) throw new Exception("LoadLibraryEx " + Marshal.GetLastWin32Error());
    ushort found = 0xFFFF;
    try { EnumResourceLanguages(m, (IntPtr)16, (IntPtr)1, (mm, t, n, l, pp) => { found = l; return false; }, IntPtr.Zero); }
    finally { FreeLibrary(m); }
    return found;
  }
  public static void Stamp(string exe, string version, string company, string product, string copyright) {
    ushort lang = FindLang(exe);
    if (lang == 0xFFFF) lang = 0; // sin recurso previo: neutro
    byte[] blob = Build(version, company, product, copyright, Path.GetFileNameWithoutExtension(exe));
    IntPtr h = BeginUpdateResource(exe, false);
    if (h == IntPtr.Zero) throw new Exception("BeginUpdateResource " + Marshal.GetLastWin32Error());
    if (!UpdateResource(h, (IntPtr)16, (IntPtr)1, lang, blob, (uint)blob.Length)) { int e = Marshal.GetLastWin32Error(); EndUpdateResource(h, true); throw new Exception("UpdateResource " + e); }
    if (!EndUpdateResource(h, false)) throw new Exception("EndUpdateResource " + Marshal.GetLastWin32Error());
    Console.WriteLine("RT_VERSION actualizado (idioma " + lang + ", " + blob.Length + " bytes)");
  }
}
'@
[VerStamp]::Stamp($Exe, $Version, $Company, $Product, $Copyright)
$vi = (Get-Item $Exe).VersionInfo
Write-Output ("Company={0} | Product={1} | FileVersion={2} | ProductVersion={3} | Desc={4}" -f $vi.CompanyName, $vi.ProductName, $vi.FileVersion, $vi.ProductVersion, $vi.FileDescription)
