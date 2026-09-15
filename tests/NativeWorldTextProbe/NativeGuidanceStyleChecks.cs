using System;
using System.IO;
using System.Text;
using JueMingR.Features.Guidance;
using JueMingR.Platform.Settings;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceStyleChecks
    {
        internal const string Legacy = "{\"format\":\"JueMingR.Guidance\",\"version\":1,\"enabled\":0}\n";
        internal static void Seed(string root)
        {
            string file = Path.Combine(root,"JueMingRData/config/features/guidance.json");
            Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,Legacy,new UTF8Encoding(false));
        }
        internal static void Codec()
        {
            var codec=new GuidancePreferenceCodec();
            string json="{\"format\":\"JueMingR.Guidance\",\"version\":2,\"enabled\":5,\"rare\":{\"rgb\":11259375,\"size\":150},\"merchant\":{\"rgb\":1193046,\"size\":60}}";
            var value=codec.Decode(Encoding.UTF8.GetBytes(json));
            Require(value.Mask==5 && (int)Get(Call(value,"Style",GuidanceKind.Rare),"Size")==150,"schema2 independent sizes and mask decode");
            Require(codec.Decode(codec.Encode(value)).Equals(value),"style preferences roundtrip");
            Require(Call(value.WithEnabled(GuidanceKind.Merchant,true),"Style",GuidanceKind.Rare).Equals(Call(value,"Style",GuidanceKind.Rare)),"toggle preserves selected styles");
            for(int mask=0;mask<8;mask++)
            {
                var old=codec.Decode(Encoding.UTF8.GetBytes(Legacy.Replace("\"enabled\":0","\"enabled\":"+mask)));
                Require(old.Mask==mask && (int)Get(Call(old,"Style",GuidanceKind.Merchant),"Size")==100,"every schema1 mask retains switches with larger default text");
            }
            foreach(string invalid in new[]{json.Replace("\"version\":2","\"version\":3"),json.Replace("\"size\":150","\"size\":49"),
                json.Replace("\"size\":150","\"size\":181"),json.Replace("\"size\":150","\"size\":150,\"unknown\":1"),
                json.Replace("\"enabled\":5","\"enabled\":5,\"enabled\":5"),json.Replace("\"size\":60", "\"other\":60")})
            {
                bool rejected=false;try{codec.Decode(Encoding.UTF8.GetBytes(invalid));}catch(PreferenceFormatException){rejected=true;}
                Require(rejected,"invalid/future/unknown preferences remain protected");
            }
        }
        internal static void Popup(object context, object host, Action<int,string> click, string root)
        {
            object popup=Get(Get(context,"Shell"),"StylePopup"), previous=null;
            foreach(GuidanceKind kind in new[]{GuidanceKind.Rare,GuidanceKind.Merchant})
            {
                var before=((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Value;
                click(2,kind==GuidanceKind.Rare?"ConfigureRare":"ConfigureMerchant");
                var target=Get(popup,"selection");var editor=Get(popup,"Editor");
                Require((bool)Get(popup,"Visible")&&(GuidanceKind)Get(popup,"GuidanceTarget")==kind,"real configuration button opens its captured target");
                Require(previous==null||!(bool)Call(target,"Same",previous),"two guidance styles never share popup identity");
                Call(editor,"BeginHex");Call(editor,"Insert",kind==GuidanceKind.Rare?"ABCDEF":"123456");
                ((Func<int,bool>)Get(target,"StepSize"))(1);
                var after=((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Value;
                Require(after.Mask==before.Mask&&(int)Get(Call(after,"Style",kind),"Size")==110&&
                    (int)Get(Call(after,"Style",kind),"Rgb")== (kind==GuidanceKind.Rare?0xABCDEF:0x123456),"color and size apply without toggling feature");
                var other=kind==GuidanceKind.Rare?GuidanceKind.Merchant:GuidanceKind.Rare;
                Require(Call(after,"Style",other).Equals(Call(before,"Style",other)),"editing one style retains the other");
                ((Action)Get(target,"Reset"))();
                Require(Call(((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Value,"Style",kind).Equals(Call(GuidancePreferences.Default,"Style",kind)),"reset restores only selected style");
                Call(editor,"BeginHex");Call(editor,"Insert",kind==GuidanceKind.Rare?"AACCFF":"FFAA88");
                ((Func<int,bool>)Get(target,"StepSize"))(1); previous=target; Call(popup,"Close");
            }
            NativeGuidanceChecks.Until(()=>((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Status==PreferenceStatus.Saved);
            Require(File.ReadAllText(Path.Combine(root,"JueMingRData/config/features/guidance.json.schema1-original"))==Legacy,"first real save archives exact validated schema1 bytes");
            Console.WriteLine("PASS: schema1 mask preservation/archive; strict schema2; two actual config buttons, independent color/size/reset and existing popup owner.");
        }
    }
}
