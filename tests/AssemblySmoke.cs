using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class AssemblySmoke {
    static int checks;
    static void Check(bool value,string name) { checks++;if(!value) throw new Exception(name); }
    static IEnumerable<TypeDefinition> Types(TypeDefinition t) { yield return t;foreach(var n in t.NestedTypes) foreach(var c in Types(n)) yield return c; }
    static Dictionary<string,MethodDefinition> Methods(AssemblyDefinition a) { return a.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToDictionary(m=>m.FullName); }
    static string InstructionText(MethodDefinition m,Instruction i) {
        string op=i.OpCode.Name; if(op.EndsWith(".s")&&i.Operand is Instruction) op=op.Substring(0,op.Length-2);
        var target=i.Operand as Instruction; if(target!=null) return op+":"+m.Body.Instructions.IndexOf(target);
        var targets=i.Operand as Instruction[]; if(targets!=null) return op+":"+string.Join(",",targets.Select(t=>m.Body.Instructions.IndexOf(t).ToString()).ToArray());
        return op+":"+(i.Operand==null?"":i.Operand.ToString());
    }
    static string Body(MethodDefinition m) { return string.Join("|",m.Body.Instructions.Select(i=>InstructionText(m,i)).ToArray()); }
    static int ModCalls(AssemblyDefinition game,string mod) { return Methods(game).Values.Sum(m=>m.Body.Instructions.Count(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).DeclaringType.Scope.Name==mod)); }
    static void Main(string[] args) {
        using(var original=AssemblyDefinition.ReadAssembly(args[0]))
        using(var combined=AssemblyDefinition.ReadAssembly(args[1]))
        using(var reinstalled=AssemblyDefinition.ReadAssembly(args[2]))
        using(var removed=AssemblyDefinition.ReadAssembly(args[3]))
        using(var standalone=AssemblyDefinition.ReadAssembly(args[4]))
        using(var mod=AssemblyDefinition.ReadAssembly(args[5])) {
            var before=Methods(original);var after=Methods(removed);var one=Methods(combined);var two=Methods(reinstalled);
            Check(before.Count==after.Count,"Removal restores original method count.");
            foreach(var m in before) Check(after.ContainsKey(m.Key)&&Body(m.Value)==Body(after[m.Key]),"Removal preserves original instructions and branch targets: "+m.Key);
            Check(one.Count==two.Count,"Reinstall method count unchanged.");
            foreach(var m in one) Check(two.ContainsKey(m.Key)&&Body(m.Value)==Body(two[m.Key]),"Reinstall idempotent: "+m.Key);
            Check(ModCalls(original,"CreativeInventory")==ModCalls(combined,"CreativeInventory"),"All Creative calls preserved during install.");
            Check(ModCalls(original,"CreativeInventory")==ModCalls(removed,"CreativeInventory"),"All Creative calls preserved during removal.");
            Check(!standalone.MainModule.AssemblyReferences.Any(r=>r.Name=="CreativeInventory"),"Standalone game has no Creative dependency.");
            Check(!mod.MainModule.AssemblyReferences.Any(r=>r.Name=="CreativeInventory"),"Controller DLL has no Creative dependency.");
            Check(!removed.MainModule.AssemblyReferences.Any(r=>r.Name=="ControllerSupport"),"Removal removes controller reference.");
            Check(ModCalls(combined,"ControllerSupport")>0&&ModCalls(standalone,"ControllerSupport")>0,"Combined and standalone hook paths populated.");
            // Unhooking modifies no movement serialization, game inventory, stats, progression or server RPC.
            Console.WriteLine("PASS: "+checks+" method preservation, independent dependencies, reinstall and removal checks.");
        }
    }
}
