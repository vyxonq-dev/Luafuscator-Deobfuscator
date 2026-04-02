using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LuafuscatorDeobf
{
    
    public static class StringClassifier
    {
        
        private static readonly HashSet<string> RobloxServices = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Players","Workspace","ReplicatedStorage","ServerStorage","ServerScriptService",
            "StarterGui","StarterPack","StarterPlayer","StarterPlayerScripts","StarterCharacterScripts",
            "Lighting","SoundService","TweenService","UserInputService","RunService",
            "HttpService","MarketplaceService","BadgeService","DataStoreService",
            "MessagingService","TeleportService","PhysicsService","CollectionService",
            "PathfindingService","ContextActionService","GuiService","LocalizationService",
            "Teams","InsertService","Chat","ReplicatedFirst","CoreGui",
            "VirtualInputManager","NetworkClient","NetworkServer","TestService",
            "VRService","HapticService","GamePassService","PointsService","AssetService",
            "AnimationProvider","Selection","ChangeHistoryService","StudioService",
            "JointsService","MemoryStoreService","AvatarEditorService",
        };

        private static readonly HashSet<string> RobloxMethods = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "GetService","WaitForChild","FindFirstChild","FindFirstAncestor",
            "FindFirstChildOfClass","FindFirstAncestorOfClass","FindFirstChildWhichIsA",
            "IsA","IsAncestorOf","IsDescendantOf",
            "GetChildren","GetDescendants","GetFullName","GetAttribute","SetAttribute",
            "Clone","Destroy","Remove","ClearAllChildren",
            "Connect","Disconnect","Wait","Once","Fire",
            "Kick","GetPlayers","GetPlayerFromCharacter","GetCharacterAppearanceInfoAsync",
            "LoadCharacter","GetFriendsAsync","GetRankInGroup","GetRoleInGroup",
            "InvokeServer","InvokeClient","FireServer","FireClient","FireAllClients",
            "GetProductInfo","GetGamePassProductInfo","PromptPurchase","PromptGamePassPurchase",
            "UpdateAsync","GetAsync","SetAsync","RemoveAsync","IncrementAsync",
            "RequestAsync","PostAsync","JSONEncode","JSONDecode",
            "TweenPosition","TweenSize","TweenSizeAndPosition","TweenColor",
            "new","fromRGB","fromHSV","fromHex","Lerp",
            "MoveTo","SetPrimaryPartCFrame","GetPrimaryPartCFrame",
            "ApplyImpulse","ChangeState","GetState",
            "Play","Stop","Pause","Resume","AdjustSpeed","AdjustVolume",
            "Raycast","FindPartOnRay","FindPartOnRayWithIgnoreList",
            "ScreenPointToRay","ViewportPointToRay",
            "BindAction","UnbindAction","GetBoundActionInfo",
            "GetMouseLocation","GetMouseDelta",
            "IsKeyDown","IsMouseButtonPressed","IsGamepadButtonDown",
            "Heartbeat","RenderStepped","Stepped","BindToRenderStep",
            "GetTouchingParts","GetConnectedParts","GetJoints","GetNetworkOwner",
            "SetNetworkOwner","GetNetworkOwnershipAuto",
            "AddTag","RemoveTag","GetTags","HasTag","GetTagged",
            "Lerp","ToObjectSpace","ToWorldSpace","PointToObjectSpace","PointToWorldSpace",
            "VectorToObjectSpace","VectorToWorldSpace","LookAt",
            "TranslateBy","RotateAroundAxis","RotateAroundWorldAxis",
            "GetPropertyChangedSignal","GetAttributeChangedSignal",
            "wait","spawn","delay","tick","time","elapsedTime",
        };

        private static readonly HashSet<string> RobloxEvents = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "PlayerAdded","PlayerRemoving","CharacterAdded","CharacterRemoving",
            "Touched","TouchEnded","ChildAdded","ChildRemoved","DescendantAdded","DescendantRemoving",
            "Changed","AttributeChanged","GetPropertyChangedSignal",
            "MouseClick","MouseButton1Click","MouseButton2Click","MouseButton1Down","MouseButton2Down",
            "MouseButton1Up","MouseButton2Up",
            "MouseEnter","MouseLeave","MouseMoved","MouseWheelForward","MouseWheelBackward",
            "InputBegan","InputEnded","InputChanged",
            "OnClientEvent","OnServerEvent","OnClientInvoke","OnServerInvoke",
            "Died","HealthChanged","StateChanged","Swimming","Climbing","Jumping","Running","Falling",
            "Activated","Deactivated","Selected","Deselected",
            "RenderStepped","Heartbeat","Stepped",
            "AncestryChanged","ChildrenChanged",
            "KeyDown","KeyUp",
        };

        private static readonly HashSet<string> RobloxProperties = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Name","Parent","Position","Size","Color","Transparency","Anchored","CanCollide",
            "Velocity","RotVelocity","AssemblyLinearVelocity","AssemblyAngularVelocity",
            "CFrame","Orientation","Rotation","PrimaryPart",
            "HumanoidRootPart","RootPart","Character","UserId","DisplayName","AccountAge",
            "Health","MaxHealth","WalkSpeed","JumpPower","JumpHeight","AutoRotate","RigType",
            "Enabled","Visible","ZIndex","BackgroundColor3","TextColor3","Text","PlaceholderText",
            "Font","FontFace","TextSize","TextWrapped","TextXAlignment","TextYAlignment",
            "TextScaled","RichText","LineHeight",
            "Image","ImageColor3","ImageTransparency","ScaleType","SliceCenter","TileSize",
            "Value","Team","TeamColor","Neutral",
            "BrickColor","Material","Reflectance","CastShadow","CollisionGroup",
            "ClassName","Description","MeshId","TextureId","SoundId","AnimationId",
            "Volume","PlaybackSpeed","Looped","Playing",
            "TweenInfo","EasingStyle","EasingDirection",
            "Lifetime","Rate","Speed","Enabled",
            "ScrollingEnabled","ScrollingDirection","CanvasPosition","CanvasSize",
            "ClipsDescendants","AutomaticSize","AutomaticCanvasSize",
            "SizeConstraint","AspectRatioConstraint","AspectType",
            "BorderSizePixel","BorderColor3","BorderMode",
            "LayoutOrder","SortOrder","Padding","FillDirection","HorizontalAlignment","VerticalAlignment",
            "Selected","Active","Selectable","SelectionOrder",
            "LocalPlayer","Character","Backpack","PlayerGui","PlayerScripts","StarterGear",
            "UserId","Name","DisplayName","AccountAge","Team","TeamColor","Neutral",
        };

        private static readonly HashSet<string> LuaBuiltins = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "print","warn","error","assert","pcall","xpcall","tostring","tonumber",
            "type","pairs","ipairs","next","select","unpack","table.unpack","rawlen",
            "rawget","rawset","rawequal","setmetatable","getmetatable",
            "load","loadstring","dofile","loadfile","require","collectgarbage",
            "string","table","math","io","os","coroutine","package","debug","bit32","utf8",
            "string.format","string.find","string.match","string.gmatch","string.gsub",
            "string.sub","string.rep","string.len","string.byte","string.char",
            "string.upper","string.lower","string.reverse","string.dump",
            "table.insert","table.remove","table.concat","table.sort","table.move",
            "table.pack","table.unpack","table.create","table.clear","table.find",
            "math.random","math.randomseed","math.floor","math.ceil","math.abs",
            "math.max","math.min","math.sqrt","math.sin","math.cos","math.tan",
            "math.asin","math.acos","math.atan","math.atan2","math.exp","math.log",
            "math.pow","math.fmod","math.modf","math.huge","math.pi","math.maxinteger",
            "os.time","os.clock","os.date","os.exit","os.getenv",
            "coroutine.create","coroutine.wrap","coroutine.resume","coroutine.yield",
            "coroutine.status","coroutine.running","coroutine.isyieldable",
            "debug.traceback","debug.getinfo","debug.sethook","debug.getlocal","debug.setlocal",
            "io.read","io.write","io.open","io.close",
            "bit32.band","bit32.bor","bit32.bxor","bit32.bnot","bit32.lshift","bit32.rshift",
            "task","task.wait","task.spawn","task.delay","task.defer","task.cancel",
            "game","workspace","script","plugin","shared",
        };

        private static readonly HashSet<string> LuafuscatorMarkers = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Tamper Detected!",
            "__LuafuscatorProx",
            "Luafuscator",
        };

        private static readonly HashSet<string> ExecutorApis = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            
            "checkcaller","checkluaclosure","isluaclosure","islclosure","iscclosure",
            "cloneref","compareinstances","getinstances","getnilinstances",
            "isrbxactive","isgameactive",
            
            "getgenv","setgenv","getfenv","setfenv","getrenv","setrenv",
            "getreg","getgc","getthreads","getconnections",
            
            "getrawmetatable","setrawmetatable","hookmetamethod",
            "setreadonly","isreadonly","makewriteable","makereadonly",
            
            "hookfunction","hookenv","hookfunc","newcclosure","newlclosure",
            "getupvalue","setupvalue","getupvalues","getupvalueid",
            "getconstant","getconstants","setconstant",
            "getproto","getprotos","setproto",
            "getstack","getstacks",
            
            "getnamecallmethod","setnamecallmethod",
            
            "readfile","writefile","appendfile","listfiles","loadfile","delfile",
            "isfile","isfolder","makefolder","delfolder",
            
            "request","http_request","syn.request","websocket.connect",
            
            "lxpcall","protectedcall","safepcall",
            "identifyexecutor","getexecutorname",
            "Drawing","isfullscreen",
            "gethwid","getuid",
            "SignalSendACK","clonefunction",
        };

        private static readonly HashSet<string> MetaMethods = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "__index","__newindex","__call","__add","__sub","__mul","__div","__mod",
            "__pow","__unm","__idiv","__band","__bor","__bxor","__bnot","__shl","__shr",
            "__concat","__len","__eq","__lt","__le","__tostring","__gc","__close",
            "__pairs","__ipairs","__metatable","__name","__mode",
            "__namecall",  
        };

        private static readonly Regex UrlPattern = new Regex(
            @"^https?://\S+|^discord\.gg/\S+|^discord\.com/\S+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LuaPatternRegex = new Regex(
            @"[%\[\]%+\*\?\.^$\(\)%-]",
            RegexOptions.Compiled);

        public static StringCategory Classify(string s)
        {
            if (string.IsNullOrEmpty(s)) return StringCategory.ShortToken;

            string trimmed = s.Trim();

            if (trimmed.Contains("Luafuscator") || LuafuscatorMarkers.Contains(trimmed))
                return StringCategory.LuafuscatorMarker;

            if (UrlPattern.IsMatch(trimmed))
                return StringCategory.URL;

            if (MetaMethods.Contains(trimmed))
                return StringCategory.MetaMethod;

            if (ExecutorApis.Contains(trimmed))
                return StringCategory.RobloxExecutor;

            if (RobloxServices.Contains(trimmed))   return StringCategory.RobloxService;
            if (RobloxMethods.Contains(trimmed))    return StringCategory.RobloxMethod;
            if (RobloxEvents.Contains(trimmed))     return StringCategory.RobloxEvent;
            if (RobloxProperties.Contains(trimmed)) return StringCategory.RobloxProperty;
            if (LuaBuiltins.Contains(trimmed))      return StringCategory.LuaBuiltin;

            if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return StringCategory.Number;

            if (trimmed.Length <= 3)
                return StringCategory.ShortToken;

            if (trimmed.Length <= 40 && LuaPatternRegex.Matches(trimmed).Count >= 2)
                return StringCategory.PatternString;

            return StringCategory.GenericString;
        }

        public static string CategoryLabel(StringCategory cat) => cat switch
        {
            StringCategory.RobloxService     => "RBX Service",
            StringCategory.RobloxMethod      => "RBX Method",
            StringCategory.RobloxEvent       => "RBX Event",
            StringCategory.RobloxProperty    => "RBX Property",
            StringCategory.LuaBuiltin        => "Lua Builtin",
            StringCategory.LuafuscatorMarker => "Obf Marker",
            StringCategory.SynapseSNC        => "Synapse SNC",
            StringCategory.RobloxExecutor    => "Executor API",
            StringCategory.MetaMethod        => "Metamethod",
            StringCategory.Number            => "Number",
            StringCategory.ShortToken        => "Token",
            StringCategory.URL               => "URL",
            StringCategory.PatternString     => "Lua Pattern",
            _                                => "String",
        };

        public static ConsoleColor CategoryColor(StringCategory cat) => cat switch
        {
            StringCategory.RobloxService     => ConsoleColor.Cyan,
            StringCategory.RobloxMethod      => ConsoleColor.Green,
            StringCategory.RobloxEvent       => ConsoleColor.Magenta,
            StringCategory.RobloxProperty    => ConsoleColor.Blue,
            StringCategory.LuaBuiltin        => ConsoleColor.DarkCyan,
            StringCategory.LuafuscatorMarker => ConsoleColor.DarkRed,
            StringCategory.SynapseSNC        => ConsoleColor.DarkMagenta,
            StringCategory.RobloxExecutor    => ConsoleColor.Red,
            StringCategory.MetaMethod        => ConsoleColor.DarkBlue,
            StringCategory.Number            => ConsoleColor.DarkYellow,
            StringCategory.ShortToken        => ConsoleColor.DarkGray,
            StringCategory.URL               => ConsoleColor.DarkGreen,
            StringCategory.PatternString     => ConsoleColor.Yellow,
            _                                => ConsoleColor.Yellow,
        };

        public static string CategoryIcon(StringCategory cat) => cat switch
        {
            StringCategory.RobloxService     => "🟦",
            StringCategory.RobloxMethod      => "🟩",
            StringCategory.RobloxEvent       => "🟣",
            StringCategory.RobloxProperty    => "🔵",
            StringCategory.LuaBuiltin        => "🔷",
            StringCategory.LuafuscatorMarker => "⛔",
            StringCategory.SynapseSNC        => "⚠️",
            StringCategory.RobloxExecutor    => "🔴",
            StringCategory.MetaMethod        => "🔧",
            StringCategory.Number            => "🔢",
            StringCategory.ShortToken        => "⬜",
            StringCategory.URL               => "🌐",
            StringCategory.PatternString     => "🔍",
            _                                => "🟨",
        };
    }
}
