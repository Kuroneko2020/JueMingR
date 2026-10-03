using System;

namespace JueMingR.Features.Combat
{
    // Display switches are deliberately independent of attack automation and
    // of each other. Radius zero tests the real mouse point against hitboxes;
    // it neither disables the display nor means an unlimited search.
    public sealed class ObservationOptions
    {
        public bool Collision {get;}
        public bool Path {get;}
        public bool ClearLine {get;}
        public bool MouseCenter {get;}
        public bool Dummy {get;}
        public int Radius {get;}
        public ObservationOptions(bool collision=false,bool path=false,bool clearLine=false,bool mouseCenter=false,bool dummy=false,int radius=25)
        {
            if(radius<0 || radius>50)throw new ArgumentOutOfRangeException(nameof(radius));
            Collision=collision;Path=path;ClearLine=clearLine;MouseCenter=mouseCenter;Dummy=dummy;Radius=radius;
        }
        public ObservationOptions Toggle(int field)
        {
            if(field<0 || field>4)throw new ArgumentOutOfRangeException(nameof(field));
            return new ObservationOptions(Collision^(field==0),Path^(field==1),ClearLine^(field==2),MouseCenter^(field==3),Dummy^(field==4),Radius);
        }
        public ObservationOptions WithRadius(int value){return new ObservationOptions(Collision,Path,ClearLine,MouseCenter,Dummy,value);}
    }
}
