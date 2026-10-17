using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// note: Contract metadata inspection alone cannot establish constructor thread safety or native execution; it identifies the smallest data-only boundary's blockers.
static class Program
{
    static readonly Dictionary<short, OpCode> Codes = BuildCodes();
    static Dictionary<short, OpCode> BuildCodes()
    {
        var result=new Dictionary<short, OpCode>();
        foreach(var field in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static))
            if(field.FieldType==typeof(OpCode)){var op=(OpCode)field.GetValue(null);result[op.Value]=op;}
        return result;
    }
    static void ConstructorCalls(Type type, HashSet<string> calls, List<string> staticFields)
    {
        var ctor=type.GetConstructor(Type.EmptyTypes);var bytes=ctor?.GetMethodBody()?.GetILAsByteArray();
        if(bytes==null)return;
        for(int index=0;index<bytes.Length;)
        {
            short value=bytes[index++];if(value==0xfe)value=(short)(0xfe00|bytes[index++]);
            var op=Codes[value];int size;
            switch(op.OperandType)
            {
                case OperandType.InlineNone:size=0;break;
                case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
                case OperandType.InlineVar:size=2;break;
                case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
                case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(bytes,index);break;
                default:size=4;break;
            }
            if(op.OperandType==OperandType.InlineMethod)
            {
                var target=ctor.Module.ResolveMethod(BitConverter.ToInt32(bytes,index));
                calls.Add(target.DeclaringType.FullName+"."+target.Name);
            }
            if(op==OpCodes.Ldsfld||op==OpCodes.Stsfld)
            {
                var field=ctor.Module.ResolveField(BitConverter.ToInt32(bytes,index));
                staticFields.Add(type.FullName+":"+op.Name+":"+field.DeclaringType.FullName+"."+field.Name);
            }
            index+=size;
        }
    }
    static void Main()
    {
        var resolver = new DefaultContractResolver();
        var converters = new JsonConverter[] { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() };
        var pending = new Queue<Type>();pending.Enqueue(typeof(WorldState));
        var seen = new HashSet<Type>();var issues = new List<string>();var approvedMemberConverters = new List<string>();var constructorCalls=new HashSet<string>();var constructorStaticFields=new List<string>();var staticInitializers=new List<string>();int objects=0,fields=0,constructors=0;
        while(pending.Count>0)
        {
            Type type=pending.Dequeue();if(type==null||!seen.Add(type))continue;
            type=Nullable.GetUnderlyingType(type)??type;
            if(type.IsEnum||type.IsPrimitive||type==typeof(string)||type==typeof(decimal)||type==typeof(DateTime)||type==typeof(Guid))continue;
            bool handled=false;foreach(var converter in converters)if(converter.CanConvert(type)){handled=true;break;}
            if(handled)continue;
            if(typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(type))continue;
            JsonContract contract=resolver.ResolveContract(type);
            if(contract.OnSerializingCallbacks.Count+contract.OnSerializedCallbacks.Count+contract.OnDeserializingCallbacks.Count+
                contract.OnDeserializedCallbacks.Count+contract.OnErrorCallbacks.Count!=0)issues.Add("callback:"+type.FullName);
            if(contract.Converter!=null)issues.Add("customConverter:"+type.FullName);
            if(typeof(UnityEngine.Object).IsAssignableFrom(type)){issues.Add("unityObject:"+type.FullName);continue;}
            if(contract is JsonArrayContract array){pending.Enqueue(array.CollectionItemType);continue;}
            if(contract is JsonDictionaryContract dictionary){pending.Enqueue(dictionary.DictionaryKeyType);pending.Enqueue(dictionary.DictionaryValueType);continue;}
            if(contract is JsonObjectContract record)
            {
                objects++;if(record.ExtensionDataGetter!=null||record.ExtensionDataSetter!=null)issues.Add("extensionData:"+type.FullName);
                if(type.GetConstructor(Type.EmptyTypes)!=null){constructors++;ConstructorCalls(type,constructorCalls,constructorStaticFields);}
                if(type.TypeInitializer!=null)staticInitializers.Add(type.FullName);
                foreach(var property in record.Properties)
                {
                    if(property.Ignored)continue;
                    fields++;pending.Enqueue(property.PropertyType);
                    FieldInfo field=property.DeclaringType.GetField(property.UnderlyingName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if(field==null)issues.Add("propertyAccessor:"+type.FullName+"."+property.PropertyName);
                    if(property.Converter!=null)
                    {
                        bool approved=property.Converter.GetType()==typeof(Vector3JsonConverter)||property.Converter.GetType()==typeof(Vector2JsonConverter)||property.Converter.GetType()==typeof(QuaternionJsonConverter);
                        string description=type.FullName+"."+property.PropertyName+":"+property.Converter.GetType().FullName;
                        if(approved)approvedMemberConverters.Add(description);else issues.Add("memberConverter:"+description);
                    }
                    if(property.ShouldSerialize!=null||property.GetIsSpecified!=null||property.SetIsSpecified!=null)issues.Add("memberCallback:"+type.FullName+"."+property.PropertyName);
                }
            }
            else issues.Add("unsupportedContract:"+type.FullName+":"+contract.GetType().Name);
        }
        Console.WriteLine(JsonConvert.SerializeObject(new {evidence="COMPILED_PRODUCTION_WORLD_DTO_CONTRACT_METADATA_ONLY",types=seen.Count,objectContracts=objects,persistedMembers=fields,parameterlessConstructors=constructors,issues,approvedMemberConverters,constructorCalls,constructorStaticFields,staticInitializers,
            constructorsAndStaticInitializationThreadSafety="NOT_VERIFIED",liveUnityDefaultSettings="NOT_OBSERVED",detachedDefaultSettingsNull=JsonConvert.DefaultSettings==null},Formatting.Indented));
    }
}
