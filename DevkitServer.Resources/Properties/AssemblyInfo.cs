using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("5b03e97d-9e96-4f25-9200-33035ccd1902")]
#if DEBUG
[assembly: InternalsVisibleTo("DevkitServer.Launcher")]
#else
[assembly: InternalsVisibleTo("DevkitServer.Launcher, PublicKey=00240000048000009400000006020000002400005253413100040000010001005514b81610f0d4950ccb290c37453cf2af725553b1e262b2b26c8b302062c334d437df5a3bc474d0feb5061cd745e4de76a701ebd0c6d0d01cf9edf7e141a22e9db2aa45e0f6a52721f3030d57e64c85f4d36a758afbef01f0d567e969e7f33bfa8a1918fbbc1cb379cb422580cef4fb1f589ad73bdd4be4d8619a677ac07dd3")]
#endif