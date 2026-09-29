# Third-party notices

SnipCanvas source and original artwork are covered by the [MIT license](LICENSE).

Windows self-contained builds bundle .NET 8.0.31, including WPF and Windows Forms.
Their MIT license and the runtime's third-party notices are included in
`licenses/dotnet-LICENSE.txt` and `licenses/dotnet-THIRD-PARTY-NOTICES.txt`.
Upstream source: [.NET runtime](https://github.com/dotnet/runtime),
[WPF](https://github.com/dotnet/wpf), and [Windows Forms](https://github.com/dotnet/winforms).
Keep these notices with redistributed Windows binaries. Update the notices when
changing the runtime version pinned in `SnipCanvas.csproj`.

The optional Windows installer is built with
[Inno Setup](https://jrsoftware.org/isinfo.php). Its compiler is a separate build
tool and is not part of the SnipCanvas source license. Use it under its
[license terms](https://github.com/jrsoftware/issrc/blob/main/license.txt).
