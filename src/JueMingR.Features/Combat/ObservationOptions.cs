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
        public bool Marker {get;}
        public bool Aim {get;}
        public int Radius {get;}
        public ObservationOptions(bool collision,bool path,bool clearLine,bool mouseCenter,bool dummy,int radius)
            :this(collision,path,clearLine,mouseCenter,dummy,radius,false){}
        public ObservationOptions(bool collision=false,bool path=false,bool clearLine=false,bool mouseCenter=false,bool dummy=false,int radius=25,bool marker=false)
            :this(collision,path,clearLine,mouseCenter,dummy,radius,marker,false){}
        public ObservationOptions(bool collision,bool path,bool clearLine,bool mouseCenter,bool dummy,int radius,bool marker,bool aim)
        {
            if(radius<0 || radius>50)throw new ArgumentOutOfRangeException(nameof(radius));
            Collision=collision;Path=path;ClearLine=clearLine;MouseCenter=mouseCenter;Dummy=dummy;Radius=radius;Marker=marker;Aim=aim;
        }
        public ObservationOptions Toggle(int field)
        {
            if(field<0 || field>6)throw new ArgumentOutOfRangeException(nameof(field));
            return new ObservationOptions(Collision^(field==0),Path^(field==1),ClearLine^(field==2),MouseCenter^(field==3),Dummy^(field==4),Radius,Marker^(field==5),Aim^(field==6));
        }
        public ObservationOptions WithRadius(int value){return new ObservationOptions(Collision,Path,ClearLine,MouseCenter,Dummy,value,Marker,Aim);}
    }
}
