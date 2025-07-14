using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DanielWillett.UITools.API;
using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.API.Extensions.Members;
using DanielWillett.UITools.Core.Extensions;
using DanielWillett.UITools.Util;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

[assembly: TypeForwardedTo(typeof(SleekExtensions))]
[assembly: TypeForwardedTo(typeof(UITools))]
[assembly: TypeForwardedTo(typeof(UIExtensionInfo))]
[assembly: TypeForwardedTo(typeof(ExistingMemberAttribute))]
[assembly: TypeForwardedTo(typeof(ExistingMemberFailureBehavior))]
[assembly: TypeForwardedTo(typeof(ExistingMemberInitializeMode))]
[assembly: TypeForwardedTo(typeof(UIExistingMemberInfo))]
[assembly: TypeForwardedTo(typeof(ContainerUIExtension))]
[assembly: TypeForwardedTo(typeof(UIExtension))]
[assembly: TypeForwardedTo(typeof(UIExtension<>))]
[assembly: TypeForwardedTo(typeof(UIExtensionAttribute))]
[assembly: TypeForwardedTo(typeof(UIExtensionInstanceInfo))]
[assembly: TypeForwardedTo(typeof(UIExtensionManager))]
[assembly: TypeForwardedTo(typeof(UIExtensionParentTypeInfo))]
[assembly: TypeForwardedTo(typeof(UIExtensionVanillaInstanceInfo))]
[assembly: TypeForwardedTo(typeof(ICustomOnCloseUIHandler))]
[assembly: TypeForwardedTo(typeof(ICustomOnDestroyUIHandler))]
[assembly: TypeForwardedTo(typeof(ICustomOnInitializeUIHandler))]
[assembly: TypeForwardedTo(typeof(ICustomOnOpenUIHandler))]
[assembly: TypeForwardedTo(typeof(ICustomUIHandler))]
[assembly: TypeForwardedTo(typeof(UIScene))]
[assembly: TypeForwardedTo(typeof(UITypeInfo))]
[assembly: TypeForwardedTo(typeof(UIVisibilityMethodInfo))]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("c0fab04d-a5ef-4a40-88ec-4f8f9fb24180")]