using System;
using System.Collections.Generic;

namespace JueMingR.TerrariaHost.F5
{
    // Only the reforge add/field and mining field/mode need an inseparable
    // leading pair. Keep the common row/label controls and all other rows alone.
    internal static class F5PairedRowLayout
    {
        internal static void Row(List<F5Element> elements,Func<string,float,F5Size> measure,ref float y,float width,string name,string button,bool fieldFirst,string[] remaining,F5RowDescription description,float fieldWidth=120)
        {
            var labelSize=measure(button,.7f);float buttonWidth=Math.Max(30,labelSize.Width+16),pairWidth=fieldWidth+4+buttonWidth;
            var temporary=new List<F5Element>();
            var actions=new string[remaining.Length+1];Array.Copy(remaining,0,actions,1,remaining.Length);
            new F5RowLayout(temporary,measure).Row(ref y,0,width,name,actions,description:description,fieldWidth:pairWidth);
            // The shared layout wraps the pair as one Field; only then expand
            // that exact slot. Rendering and hit projection consume these same
            // final elements, with no transparent or stale extra button.
            foreach(var e in temporary)
            {
                if(e.Kind!=F5ElementKind.Field){elements.Add(e);continue;}
                var r=e.Rect;
                if(labelSize.Height+8>r.Height)throw new InvalidOperationException("F5 paired action font exceeds its row.");
                float fieldX=fieldFirst?r.X:r.X+buttonWidth+4,buttonX=fieldFirst?r.X+fieldWidth+4:r.X;
                var field=new F5Element(F5ElementKind.Field,new F5Rect(fieldX,r.Y,fieldWidth,r.Height),null,default(F5Size),.7f,F5Command.None);
                var action=new F5Element(F5ElementKind.Button,new F5Rect(buttonX,r.Y,buttonWidth,r.Height),button,labelSize,.7f,F5Command.None);
                if(fieldFirst){elements.Add(field);elements.Add(action);}else{elements.Add(action);elements.Add(field);}
            }
        }
    }
}
