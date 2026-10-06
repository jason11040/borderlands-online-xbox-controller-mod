using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

class ControllerPatcher
{
    const string ModName="ControllerSupport";
    static int Main(string[] args)
    {
        try {
            if(args.Length<3) throw new ArgumentException("Usage: ControllerPatcher.exe patch|remove|verify <Assembly-CSharp.dll> <ControllerSupport.dll> [output.dll]");
            var resolver=new DefaultAssemblyResolver(); resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
            resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2])));
            using(var game=AssemblyDefinition.ReadAssembly(args[1],new ReaderParameters { AssemblyResolver=resolver }))
            using(var mod=AssemblyDefinition.ReadAssembly(args[2],new ReaderParameters { AssemblyResolver=resolver })) {
                var module=game.MainModule; var addon=mod.MainModule.Types.Single(t=>t.FullName=="StandaloneController.ControllerSupport");
                if(args[0]=="verify") { Verify(game); Console.WriteLine("Controller hooks verified; all references resolve to separate ControllerSupport.dll."); return 0; }
                if(args.Length!=4) throw new ArgumentException("An output path is required.");
                // Never overwrite the file being inspected; installation commits the verified output later.
                if(Path.GetFullPath(args[1])==Path.GetFullPath(args[3])) throw new ArgumentException("Output must be a different file.");
                var before=Methods(module).ToDictionary(m=>m.FullName,m=>Signature(m));
                RemoveHooks(game);
                if(args[0]=="patch") {
                    var app=module.Types.Single(t=>t.Name=="AppManager"); var input=module.Types.Single(t=>t.Name=="InputManager");
                    Prefix(app.Methods.Single(m=>m.Name=="Update"),Import(module,addon,"Tick"),false);
                    Return(app.Methods.Single(m=>m.Name=="OnGUI"),Import(module,addon,"Draw"),-1);
                    Prefix(app.Methods.Single(m=>m.Name=="OnApplicationQuit"),Import(module,addon,"Shutdown"),false);
                    var focus=app.Methods.FirstOrDefault(m=>m.Name=="OnApplicationFocus");
                    if(focus==null) {
                        focus=new MethodDefinition("OnApplicationFocus",MethodAttributes.Private|MethodAttributes.HideBySig,module.TypeSystem.Void);
                        focus.Parameters.Add(new ParameterDefinition("focused",ParameterAttributes.None,module.TypeSystem.Boolean));
                        focus.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret)); app.Methods.Add(focus);
                    }
                    Prefix(focus,Import(module,addon,"FocusChanged"),true);
                    foreach(var hook in new[]{new[]{"method_4","Held"},new[]{"method_5","Axis"},new[]{"method_6","Released"},new[]{"method_7","Pressed"},new[]{"method_8","Released"},new[]{"method_9","Pressed"}})
                        Return(input.Methods.Single(m=>m.Name==hook[0]),Import(module,addon,hook[1]),1);
                    Return(module.Types.Single(t=>t.Name=="EntityWeapon").Methods.Single(m=>m.Name=="method_35"),Import(module,addon,"Shot"),0);
                    var behavior=module.Types.Single(t=>t.Name=="PlayerBehavior");
                    var look=behavior.Methods.Single(m=>m.Name=="method_37"); var lookIl=look.Body.GetILProcessor();
                    foreach(var ret in look.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
                        var first=lookIl.Create(OpCodes.Ldarg_1); Retarget(look,ret,first);lookIl.InsertBefore(ret,first);
                        lookIl.InsertBefore(ret,lookIl.Create(OpCodes.Ldind_R4));lookIl.InsertBefore(ret,lookIl.Create(OpCodes.Ldarg_2));
                        lookIl.InsertBefore(ret,lookIl.Create(OpCodes.Ldind_R4));lookIl.InsertBefore(ret,lookIl.Create(OpCodes.Ldarg_0));
                        lookIl.InsertBefore(ret,lookIl.Create(OpCodes.Ldfld,behavior.Fields.Single(f=>f.Name=="m_inverseMouse")));
                        lookIl.InsertBefore(ret,lookIl.Create(OpCodes.Call,Import(module,addon,"ObserveLook")));
                    }
                    ExpandBranches(look);
                    var menuUpdate=module.Types.Single(t=>t.Name=="UniUIManager").Methods.Single(m=>m.Name=="Update");
                    var menuIl=menuUpdate.Body.GetILProcessor();int mouseHooks=0,scrollHooks=0;
                    foreach(var instruction in menuUpdate.Body.Instructions.ToArray()) {
                        var native=instruction.Operand as MethodReference;
                        if(native==null||native.DeclaringType.FullName!="UnityEngine.Input") continue;
                        if(native.Name=="GetMouseButtonDown"||native.Name=="GetMouseButtonUp") {
                            menuIl.InsertAfter(instruction,menuIl.Create(OpCodes.Call,Import(module,addon,"FilterMouse")));mouseHooks++;
                        } else if(native.Name=="GetAxis") {
                            menuIl.InsertAfter(instruction,menuIl.Create(OpCodes.Call,Import(module,addon,"FilterScroll")));scrollHooks++;
                        }
                    }
                    if(mouseHooks!=3||scrollHooks!=1) throw new InvalidOperationException("Unexpected legacy menu input layout.");
                    ExpandBranches(menuUpdate);
                    Verify(game);
                } else if(args[0]!="remove") throw new ArgumentException("Unknown mode: "+args[0]);
                // No controller patch can touch movement/network/weapon stats or another mod's logic.
                var allowed=new HashSet<string>(new[]{"AppManager::Update","AppManager::OnGUI","AppManager::OnApplicationQuit","AppManager::OnApplicationFocus",
                    "InputManager::method_4","InputManager::method_5","InputManager::method_6","InputManager::method_7","InputManager::method_8","InputManager::method_9","EntityWeapon::method_35","PlayerBehavior::method_37","UniUIManager::Update"});
                foreach(var method in Methods(module)) {
                    string old; if(before.TryGetValue(method.FullName,out old)&&old!=Signature(method)&&!allowed.Contains(method.DeclaringType.Name+"::"+method.Name))
                        throw new InvalidOperationException("Unexpected change: "+method.FullName);
                }
                game.Write(args[3]);
            }
            // Reopen the saved PE, checking every hook survived serialization.
            if(args[0]=="patch") using(var check=AssemblyDefinition.ReadAssembly(args[3])) Verify(check);
            Console.WriteLine(args[0]+" successful: "+Path.GetFullPath(args[3])); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static IEnumerable<MethodDefinition> Methods(ModuleDefinition module) { return module.Types.SelectMany(Recursive).SelectMany(t=>t.Methods).Where(m=>m.HasBody); }
    static IEnumerable<TypeDefinition> Recursive(TypeDefinition t) { yield return t; foreach(var child in t.NestedTypes) foreach(var sub in Recursive(child)) yield return sub; }
    static bool IsHook(Instruction i) { var r=i.Operand as MethodReference; return r!=null&&r.DeclaringType.FullName=="StandaloneController.ControllerSupport"; }
    static MethodReference Import(ModuleDefinition m,TypeDefinition addon,string name) { return m.ImportReference(addon.Methods.Single(x=>x.Name==name)); }
    static string Signature(MethodDefinition m)
    {
        // Instruction indices retain branch targets while ignoring renumbered byte offsets.
        return string.Join("|",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+Operand(m,i.Operand)).ToArray());
    }
    static string Operand(MethodDefinition m,object op) {
        var ins=op as Instruction; if(ins!=null) return "@"+m.Body.Instructions.IndexOf(ins);
        var list=op as Instruction[]; if(list!=null) return string.Join(",",list.Select(i=>"@"+m.Body.Instructions.IndexOf(i)).ToArray());
        return op==null?"":op.ToString();
    }
    static void Retarget(MethodDefinition m,Instruction old,Instruction next)
    {
        foreach(var ins in m.Body.Instructions) {
            if(ins.Operand==old) ins.Operand=next;
            var targets=ins.Operand as Instruction[]; if(targets!=null) for(int k=0;k<targets.Length;k++) if(targets[k]==old) targets[k]=next;
        }
        foreach(var handler in m.Body.ExceptionHandlers) {
            if(handler.TryStart==old) handler.TryStart=next; if(handler.TryEnd==old) handler.TryEnd=next;
            if(handler.HandlerStart==old) handler.HandlerStart=next; if(handler.HandlerEnd==old) handler.HandlerEnd=next;
            if(handler.FilterStart==old) handler.FilterStart=next;
        }
    }
    static void Return(MethodDefinition host,MethodReference hook,int argument)
    {
        var il=host.Body.GetILProcessor();
        foreach(var ret in host.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
            var first=argument==0?il.Create(OpCodes.Ldarg_0):argument==1?il.Create(OpCodes.Ldarg_1):il.Create(OpCodes.Call,hook);
            Retarget(host,ret,first); il.InsertBefore(ret,first);
            if(argument>=0) il.InsertBefore(ret,il.Create(OpCodes.Call,hook));
        }
        ExpandBranches(host);
    }
    static void Prefix(MethodDefinition host,MethodReference hook,bool parameter)
    {
        var il=host.Body.GetILProcessor(); var first=host.Body.Instructions[0];
        if(parameter) il.InsertBefore(first,il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first,il.Create(OpCodes.Call,hook)); ExpandBranches(host);
    }
    static void RemoveHooks(AssemblyDefinition game)
    {
        foreach(var method in Methods(game.MainModule).ToArray()) {
            var il=method.Body.GetILProcessor();
            foreach(var call in method.Body.Instructions.Where(IsHook).ToArray()) {
                var r=(MethodReference)call.Operand;
                if(r.Name=="ObserveLook") {
                    var block=new List<Instruction>();var cursor=call;
                    for(int n=0;n<7;n++) { block.Add(cursor);cursor=cursor.Previous; }
                    var codes=new[]{Code.Ldarg_1,Code.Ldind_R4,Code.Ldarg_2,Code.Ldind_R4,Code.Ldarg_0,Code.Ldfld,Code.Call};block.Reverse();
                    if(block.Where((i,n)=>i.OpCode.Code!=codes[n]).Any()) throw new InvalidOperationException("Unrecognized look hook.");
                    foreach(var instruction in block) { Retarget(method,instruction,call.Next);il.Remove(instruction); }
                    continue;
                }
                bool load=r.Name=="FocusChanged"||r.Name=="Shot"||r.Name=="Held"||r.Name=="Pressed"||r.Name=="Released"||r.Name=="Axis";
                var first=load?call.Previous:call;
                if(load && first.OpCode!=OpCodes.Ldarg_0&&first.OpCode!=OpCodes.Ldarg_1) throw new InvalidOperationException("Unrecognized controller hook.");
                var next=call.Next; if(next==null) throw new InvalidOperationException("Controller hook has no continuation.");
                Retarget(method,first,next); Retarget(method,call,next);
                if(load) il.Remove(first); il.Remove(call);
            }
            // Only remove the empty Unity callback this patcher originally added.
            if(method.Name=="OnApplicationFocus"&&method.DeclaringType.Name=="AppManager"&&method.Body.Instructions.Count==1&&method.Body.Instructions[0].OpCode==OpCodes.Ret)
                method.DeclaringType.Methods.Remove(method);
        }
        foreach(var reference in game.MainModule.AssemblyReferences.Where(r=>r.Name==ModName).ToArray()) game.MainModule.AssemblyReferences.Remove(reference);
    }
    static void Verify(AssemblyDefinition game)
    {
        var all=Methods(game.MainModule).ToArray();
        var expected=new Dictionary<string,string> {
            {"AppManager::Update","Tick"},{"AppManager::OnGUI","Draw"},{"AppManager::OnApplicationQuit","Shutdown"},{"AppManager::OnApplicationFocus","FocusChanged"},
            {"InputManager::method_4","Held"},{"InputManager::method_5","Axis"},{"InputManager::method_6","Released"},{"InputManager::method_7","Pressed"},{"InputManager::method_8","Released"},{"InputManager::method_9","Pressed"},
            {"EntityWeapon::method_35","Shot"},{"PlayerBehavior::method_37","ObserveLook"},{"UniUIManager::Update","MenuFilters"}
        };
        foreach(var entry in expected) {
            var host=all.Single(m=>m.DeclaringType.Name+"::"+m.Name==entry.Key);
            var calls=host.Body.Instructions.Where(IsHook).ToArray();
            if(entry.Value=="MenuFilters") {
                if(calls.Length!=4||calls.Count(c=>((MethodReference)c.Operand).Name=="FilterMouse")!=3||calls.Count(c=>((MethodReference)c.Operand).Name=="FilterScroll")!=1)
                    throw new InvalidOperationException("Menu input shielding hooks missing/duplicated.");
                continue;
            }
            int wanted=(entry.Value=="Tick"||entry.Value=="Shutdown"||entry.Value=="FocusChanged")?1:host.Body.Instructions.Count(i=>i.OpCode==OpCodes.Ret);
            if(calls.Length!=wanted||calls.Any(c=>((MethodReference)c.Operand).Name!=entry.Value)) throw new InvalidOperationException("Hook missing/duplicated: "+entry.Key);
            foreach(var c in calls) if(((MethodReference)c.Operand).DeclaringType.Scope.Name!=ModName) throw new InvalidOperationException("Wrong mod dependency.");
        }
        if(all.Where(m=>!expected.ContainsKey(m.DeclaringType.Name+"::"+m.Name)).Any(m=>m.Body.Instructions.Any(IsHook))) throw new InvalidOperationException("Unexpected controller hook.");
        if(game.MainModule.AssemblyReferences.Count(r=>r.Name==ModName)!=1) throw new InvalidOperationException("Controller assembly reference missing or duplicated.");
    }
    static void ExpandBranches(MethodDefinition m)
    {
        foreach(var i in m.Body.Instructions) {
            switch(i.OpCode.Code) {
                case Code.Br_S:i.OpCode=OpCodes.Br;break;case Code.Brfalse_S:i.OpCode=OpCodes.Brfalse;break;case Code.Brtrue_S:i.OpCode=OpCodes.Brtrue;break;
                case Code.Beq_S:i.OpCode=OpCodes.Beq;break;case Code.Bne_Un_S:i.OpCode=OpCodes.Bne_Un;break;
                case Code.Bge_S:i.OpCode=OpCodes.Bge;break;case Code.Bge_Un_S:i.OpCode=OpCodes.Bge_Un;break;
                case Code.Bgt_S:i.OpCode=OpCodes.Bgt;break;case Code.Bgt_Un_S:i.OpCode=OpCodes.Bgt_Un;break;
                case Code.Ble_S:i.OpCode=OpCodes.Ble;break;case Code.Ble_Un_S:i.OpCode=OpCodes.Ble_Un;break;
                case Code.Blt_S:i.OpCode=OpCodes.Blt;break;case Code.Blt_Un_S:i.OpCode=OpCodes.Blt_Un;break;case Code.Leave_S:i.OpCode=OpCodes.Leave;break;
            }
        }
    }
}
