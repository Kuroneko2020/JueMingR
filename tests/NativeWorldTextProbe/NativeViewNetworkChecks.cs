using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Original dedicated server + original native TCP client/handshake/decoder,
    // driven in an initialized native Main by a bounded network pump. The
    // original-view specialist owns device/content and optional real Draw.
    internal static class NativeViewNetworkChecks
    {
        private const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly List<Point> requests=new List<Point>();
        private static readonly Dictionary<int,int> received=new Dictionary<int,int>();
        private static readonly HashSet<int> states=new HashSet<int>();
        private static MethodInfo update,clear;
        private static int feedback;
        private static string root;
        private static object connectingConsumer;
        private static bool injectSendFailure;
        internal static Point ReceivedSection {get;private set;}
        internal static int Run(string content,string output)
        {
            Environment.SetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_ONLY","1");
            return NativeViewOriginalChecks.Run(content,output);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void RunOnInitializedMain(string content,string output,Action drawWitness=null)
        {
            requests.Clear();received.Clear();states.Clear();feedback=0;injectSendFailure=false;ReceivedSection=Point.Zero;
            root=Path.GetFullPath(output);Directory.CreateDirectory(root);
            Terraria.Program.SavePath=Path.Combine(root,"client-user");Directory.CreateDirectory(Terraria.Program.SavePath);
            Require(Hash(typeof(Main).Assembly.Location)=="960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3","locked original .8 client assembly");
            NativeTestAudio.Install(typeof(Main).Assembly);
            var audit=new Harmony("JueMingR.Tests.ViewNetwork");Process server=null;StreamWriter serverLog=null;
            string oldDirectory=Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory=root;
                Main.myPlayer=0;Main.player[0]=CreateNativePlayer();Main.clientPlayer=new Player();
                Main.ActivePlayerFileData=new Terraria.IO.PlayerFileData(Path.Combine(Terraria.Program.SavePath,"fixture.plr"),false){Player=Main.LocalPlayer};
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath,"fixture.wld"),false){UniqueId=Guid.NewGuid()};
                Main.gameMenu=true;Main.mapEnabled=false;Main.autoSave=false;Main.autoPass=true;
                Main.clientUUID=Guid.NewGuid().ToString();
                Netplay.Initialize();
                audit.Patch(typeof(NetMessage).GetMethod("SendPacket",F),prefix:new HarmonyMethod(typeof(NativeViewNetworkChecks),nameof(Sending)));
                audit.Patch(typeof(MessageBuffer).GetMethod("GetData",F),postfix:new HarmonyMethod(typeof(NativeViewNetworkChecks),nameof(Received)));
                audit.Patch(typeof(NetMessage).GetMethod("SendData",F),prefix:new HarmonyMethod(typeof(NativeViewNetworkChecks),nameof(SendFailure)));
                Console.WriteLine("NETWORK boundary: initialized native Main TCP client; original cloud/world initialization retained; no fake handshake or client-state promotion.");
                string host=Environment.GetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_CANDIDATE");Require(!String.IsNullOrEmpty(host),"explicit fixed network candidate required");
                var candidate=Assembly.LoadFrom(host);
                Console.WriteLine("NETWORK fixed candidate="+Path.GetFullPath(host)+" sha256="+Hash(host)+" mvid="+candidate.ManifestModule.ModuleVersionId);
                Console.WriteLine("NETWORK original client="+typeof(Main).Assembly.Location+" sha256="+Hash(typeof(Main).Assembly.Location)+" mvid="+typeof(Main).Assembly.ManifestModule.ModuleVersionId);
                var type=candidate.GetType("JueMingR.TerrariaHost.View.ViewSectionRequests",true);
                update=type.GetMethod("Update",F);clear=type.GetMethod("Clear",F);
                var consumer=New(type);connectingConsumer=consumer;
                Invoke(consumer,0,true);Require(requests.Count==0,"ordinary non-client setup performs no section request");
                int port=FreePort();server=StartServer(content,port,out serverLog);
                WaitForServer(server,port);
                Require(Netplay.SetRemoteIP("127.0.0.1"),"original client accepts loopback address");Netplay.ListenPort=port;
                Netplay.StartTcpClient();
                PumpUntil(()=>Netplay.Connection.State==10 && Count(129)>0,90,"native handshake/client spawn and server entry acknowledgement");
                Require(Count(3)>0 && Count(7)>0 && Count(10)>0 && Count(49)>0 && Count(129)>0,"real native assignment/world/tile/spawn/server-entry packets were received");
                Console.WriteLine("NETWORK native TCP handshake states="+String.Join(",",states.OrderBy(x=>x))+" assigned="+Main.myPlayer+" world="+Main.maxTilesX+"x"+Main.maxTilesY+" serverPid="+server.Id);
                int width=Main.screenWidth,height=Main.screenHeight;var matrix=Main.GameViewMatrix;var position=Main.screenPosition;
                try{CheckConsumers(type,consumer);}
                finally{Main.screenWidth=width;Main.screenHeight=height;Main.GameViewMatrix=matrix;Main.screenPosition=position;}
                drawWitness?.Invoke();
                Console.WriteLine("PASS VIEW NETWORK: original dedicated server + initialized native TCP client handshake/dispatcher/decoder; production requests, loaded/unknown, ACK dedup, boundaries,16 in-flight and300-update timeout. Native full-client Update/Draw and owner acceptance require their own evidence.");
            }
            finally
            {
                // The native loop may save only the fixture's own player on
                // disconnect. Marking this as menu avoids an unrelated save;
                // no foreign process or path is ever an exit target.
                injectSendFailure=false;connectingConsumer=null;Main.gameMenu=true;Netplay.Disconnect=true;
                var until=Stopwatch.StartNew();while(Netplay.ClientLoopThreadRunning && until.ElapsedMilliseconds<5000)Thread.Sleep(10);
                Netplay.Connection.Socket.Close();
                audit.UnpatchAll(audit.Id);
                try{if(server!=null)StopOwnedServer(server);}
                finally{serverLog?.Dispose();Environment.CurrentDirectory=oldDirectory;}
            }
        }
        private static Player CreateNativePlayer()
        {
            var player=new Player{active=true,whoAmI=0,name="ViewNetworkFixture",SpawnX=-1,SpawnY=-1};
            // Player's constructor alone is not a created/loaded native player.
            // Reuse the real new-character setup, including its entire per-player
            // persistent creative state. Packet49 Spawn consumes that state even
            // for a classic character; no power or network state is substituted.
            var creation=new Terraria.GameContent.UI.States.UICharacterCreation(player);
            typeof(Terraria.GameContent.UI.States.UICharacterCreation).GetMethod("SetupPlayerStatsAndInventoryBasedOnDifficulty",F).Invoke(creation,null);
            Require(player.savedPerPlayerFieldsThatArentInThePlayerClass!=null,"original new-character initialization completes saved per-player state");
            return player;
        }
        private static void StopOwnedServer(Process server)
        {
            try
            {
                if(!server.HasExited)
                {
                    // Locked original Program.InitializeConsoleOutput selects
                    // UTF-16LE input on Windows. Write those bytes without a
                    // preamble; the redirected parent's default UTF-8 is wrong.
                    try{byte[] command=System.Text.Encoding.Unicode.GetBytes("exit-nosave\r\n");server.StandardInput.BaseStream.Write(command,0,command.Length);server.StandardInput.BaseStream.Flush();}
                    catch(IOException error){Console.WriteLine("NETWORK owned server command pipe closed: "+error.Message);}
                }
                if(!server.WaitForExit(15000)){Console.WriteLine("OWNED SERVER normal exit timed out; terminating exact test pid="+server.Id);server.Kill();Require(server.WaitForExit(5000),"owned server terminates after exact-process fallback");}
                // Drain this process's asynchronous stdout before disposing its sink.
                server.WaitForExit();Console.WriteLine("NETWORK owned server stopped pid="+server.Id+" exit="+server.ExitCode);
            }
            finally{server.Dispose();}
        }
        private static object New(Type type){return Activator.CreateInstance(type,F,null,new object[]{(Action<string>)(text=>{feedback++;Console.WriteLine("NETWORK production feedback: "+text);})},null);}
        private static void Invoke(object value,ulong tick,bool expanded){update.Invoke(value,new object[]{tick,expanded});}
        private static int Count(int message){int count;return received.TryGetValue(message,out count)?count:0;}
        private static void Sending(byte[] data)
        {
            if(data.Length>=7 && data[2]==MessageID.RequestSection)
            {var point=new Point(BitConverter.ToUInt16(data,3),BitConverter.ToUInt16(data,5));requests.Add(point);Console.WriteLine("NETWORK actual RequestSection159="+point);}
        }
        private static void Received(int messageType)
        {
            int value;received.TryGetValue(messageType,out value);received[messageType]=value+1;
        }
        private static void SendFailure(int msgType)
        {if(injectSendFailure && msgType==MessageID.RequestSection)throw new IOException("Controlled native section-send failure");}
        private static void Pump()
        {
            typeof(Main).GetMethod("ConsumeAllMainThreadActions",F).Invoke(null,null);
            Netplay.UpdateInMainThread();states.Add(Netplay.Connection.State);
            if(connectingConsumer!=null && Netplay.Connection.State<10)
            {int before=requests.Count;Invoke(connectingConsumer,0,true);Require(requests.Count==before,"actual pre-entry native connection states do not authorize section requests");}
        }
        private static void PumpUntil(Func<bool> done,int seconds,string purpose)
        {
            var time=Stopwatch.StartNew();while(!done())
            {
                Pump();if(Netplay.Disconnect)throw new InvalidOperationException("Native disconnect while "+purpose+": "+Main.statusText);
                if(time.Elapsed.TotalSeconds>seconds)throw new TimeoutException(purpose+" state="+Netplay.Connection.State+" status="+Main.statusText);
                Thread.Sleep(2);
            }
        }
        private static void Camera(Point section,int width=800,int height=600)
        {
            Main.screenWidth=width;Main.screenHeight=height;Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);
            Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,width,height));Main.GameViewMatrix.Zoom=new Vector2(.5f);
            Main.screenPosition=new Vector2(section.X*3200+1600-width*.5f,section.Y*2400+1200-height*.5f);
        }
        private static void CheckConsumers(Type type,object consumer)
        {
            Point cold=Point.Zero;bool found=false;
            for(int y=1;y<Main.maxSectionsY-1 && !found;y++)for(int x=1;x<Main.maxSectionsX-1;x++)
                if(!Main.sectionManager.SectionLoaded(x,y)){cold=new Point(x,y);found=true;break;}
            Require(found,"actual joined client has a cold section outside initial received coverage");Camera(cold);
            var reader=Assembly.LoadFrom(Environment.GetEnvironmentVariable("JUEMINGR_VIEW_NETWORK_CANDIDATE")).GetType("JueMingR.TerrariaHost.World.WorldTileObservation",true);
            int tileX=cold.X*200+100,tileY=cold.Y*150+75;
            var unknown=reader.GetMethod("ReadCurrent",F).Invoke(null,new object[]{tileX,tileY});Require(!(bool)Get(unknown,"Readable"),"production observer retains unknown before real network receipt");
            int before=requests.Count;
            Main.mapFullscreen=true;Invoke(consumer,1,true);Main.mapFullscreen=false;
            Main.inFancyUI=true;Invoke(consumer,2,true);Main.inFancyUI=false;
            Main.onlyDrawFancyUI=true;Invoke(consumer,2,true);Main.onlyDrawFancyUI=false;
            Invoke(consumer,3,false);Require(requests.Count==before,"fullscreen map/fancy UI/non-expanded states do not request");
            Invoke(consumer,4,true);Require(requests.Count==before+1 && requests.Last()==cold,"production consumer sends exactly the current cold section over actual native TCP");
            Invoke(consumer,5,true);Require(requests.Count==before+1 && !Main.sectionManager.SectionLoaded(cold.X,cold.Y),"pending section is deduplicated and never marked loaded before decoder");
            PumpUntil(()=>Main.sectionManager.SectionLoaded(cold.X,cold.Y),15,"actual159 server reply10/client decoder acknowledgement");
            ReceivedSection=cold;
            var known=reader.GetMethod("ReadCurrent",F).Invoke(null,new object[]{tileX,tileY});Require((bool)Get(known,"Readable") && Main.tile[tileX,tileY]!=null,"actual decoder publishes readable terrain");
            for(ulong tick=6;tick<106;tick++)Invoke(consumer,tick,true);
            Require(requests.Count==before+1 && Pending(consumer)==0,"100 stable loaded expanded-view updates add no request and retire acknowledged slots");
            Console.WriteLine("NETWORK true receipt section="+cold+" Tile="+Main.tile[tileX,tileY].type+" wall="+Main.tile[tileX,tileY].wall+" liquid="+Main.tile[tileX,tileY].liquid+" packets10="+Count(10));
            // Hold main-thread decode, not the socket. These bounded assertions
            // exercise real production dispatch while TCP replies are in flight;
            // the synthetic update clock is not a measured network timeout.
            clear.Invoke(consumer,null);Camera(new Point(2,Main.maxSectionsY-3),6400,4800);
            before=requests.Count;
            for(ulong tick=110;tick<140;tick++)
            {int prior=requests.Count;Invoke(consumer,tick,true);Require(requests.Count-prior<=1,"production sends at most one real packet in one completed update");}
            Require(requests.Count-before==16 && Pending(consumer)==16,"real sender reaches but never exceeds16 outstanding without main-thread ACK consumption");
            Console.WriteLine("NETWORK controlled ACK delay requests="+(requests.Count-before)+" pending="+Pending(consumer));
            Camera(new Point(Main.maxSectionsX-3,1));int previous=requests.Count;Invoke(consumer,150,true);
            Require(requests.Count==previous+1 && Pending(consumer)==1,"leaving the old viewport cancels local tracking so a full old16 cannot block the new view");
            clear.Invoke(consumer,null);Camera(new Point(Main.maxSectionsX-2,Main.maxSectionsY-2));
            before=requests.Count;int oldFeedback=feedback;Invoke(consumer,200,true);
            Require(requests.Count==before+1,"timeout witness starts one real pending request");
            Invoke(consumer,500,true);for(ulong tick=501;tick<550;tick++)Invoke(consumer,tick,true);
            Require(feedback==oldFeedback+1 && requests.Count==before+1,"300 controlled update ticks stop new requests once, with no repeated retry/feedback");
            Invoke(consumer,551,false);Require(Pending(consumer)==0,"normal view exits extra in-flight tracking");
            Camera(new Point(Main.maxSectionsX-2,1));oldFeedback=feedback;before=requests.Count;
            injectSendFailure=true;try{Invoke(consumer,600,true);}finally{injectSendFailure=false;}
            Invoke(consumer,601,true);
            Require(feedback==oldFeedback+1 && requests.Count==before,"native send failure stays local, reports once and stops retrying");
            Pump();
            Console.WriteLine("NETWORK actual entity receipts NPC23="+Count(23)+" liveNPC="+Main.npc.Count(n=>n!=null && n.active)+" item21="+Count(21)+" projectile27="+Count(27)+"; no forced distant entity activation.");
            Console.WriteLine("NETWORK production source/ACK/timeout boundary checks complete; controlled ACK delays are not ordinary network latency measurements.");
        }
        private static int Pending(object consumer)
        {return ((Array)Get(consumer,"pending")).Cast<object>().Count(slot=>(bool)Get(slot,"Used"));}
        private static int FreePort()
        {var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();return port;}
        private static Process StartServer(string content,int port,out StreamWriter log)
        {
            string serverPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(content)),"TerrariaServer.exe");
            Require(Hash(serverPath)=="328872C65A4A7A94F050EAD1566F1567AF15C3193D4D7008A460507D3686D7C2","locked original server binary");
            Console.WriteLine("NETWORK original server="+serverPath+" sha256="+Hash(serverPath));
            string save=Path.Combine(root,"server-user");Directory.CreateDirectory(save);
            string config=Path.Combine(root,"server-config.txt");
            File.WriteAllText(config,"world="+Path.Combine(save,"view-network.wld")+"\nautocreate=1\nworldname=VIEW network fixture\nseed=JMR-VIEW-20261018\nport="+port+"\nmaxplayers=2\nupnp=0\nlanguage=en-US\nsecure=1\n",new System.Text.UTF8Encoding(false));
            log=new StreamWriter(Path.Combine(root,"server.log"),true){AutoFlush=true};var sink=log;
            var process=new Process{StartInfo=new ProcessStartInfo(serverPath,"-config \""+config+"\" -savedirectory \""+save+"\" -noupnp -ip 127.0.0.1")
                {WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
            process.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lock(sink)sink.WriteLine(e.Data);};
            process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(sink)sink.WriteLine("STDERR "+e.Data);};
            Require(process.Start(),"start only owned original server");process.BeginOutputReadLine();process.BeginErrorReadLine();
            File.WriteAllText(Path.Combine(root,"owned-server-pid.txt"),process.Id.ToString());
            Console.WriteLine("NETWORK owned original server pid="+process.Id+" port="+port+" save="+save);return process;
        }
        private static void WaitForServer(Process process,int port)
        {
            var time=Stopwatch.StartNew();while(time.Elapsed.TotalSeconds<180)
            {
                if(process.HasExited)throw new InvalidOperationException("Owned server exited="+process.ExitCode+"; see server.log");
                try{using(var client=new TcpClient()){client.Connect(IPAddress.Loopback,port);}Console.WriteLine("NETWORK original server listening after-ms="+time.ElapsedMilliseconds);return;}
                catch(SocketException){Thread.Sleep(200);}
            }
            throw new TimeoutException("Owned original server startup; see server.log");
        }
        private static string Hash(string path)
        {using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");}
    }
}
