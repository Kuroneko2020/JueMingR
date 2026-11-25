using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionSlotVisualChecks
    {
        private static object Slot(object window, bool owner, int slot, string area="Inventory")
        {
            string key="slot:"+owner+":"+area+":"+slot+"/";
            return ((IEnumerable)Get(window,"controls")).Cast<object>().Single(c=>((string)Get(c,"Key")).StartsWith(key,StringComparison.Ordinal));
        }

        private static void Frame(object context, Vector2 p, bool right, bool left = false, params Keys[] keys)
        {
            var shell=Get(context,"Shell"); var input=Get(context,"Input");
            Call(input,"BeginUpdate"); Call(shell,"BeforeInput");
            PlayerInput.MouseInfo=new MouseState((int)p.X,(int)p.Y,0,left?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,
                right?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released);
            PlayerInput.ScrollWheelDelta=PlayerInput.ScrollWheelDeltaForUI=0;
            PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft=left; PlayerInput.Triggers.Current.MouseRight=right;
            PlayerInput.Triggers.Update(); Main.mouseX=(int)p.X; Main.mouseY=(int)p.Y; Main.mouseRight=right; Main.mouseLeft=left;
            Call(input,"AfterMapping"); Main.keyState=new KeyboardState(keys); Call(input,"AfterKeyboardRefresh");
            Call(shell,"ProcessInput"); Call(context,"UpdateRuntime"); Call(context,"UpdateShell");
            Require(!(bool)Get(shell,"Failed"),"production companion right-click input frame");
        }

        internal static void Run(object context, object window, object host, Guid id, ProbeGraphics graphics, string output, Matrix matrix, Vector2 screen)
        {
            var human=Main.LocalPlayer; var body=(Player)Call(host,"GetBody",id); var shell=Get(context,"Shell");
            Item oldOwner=human.inventory[0], oldTarget=human.inventory[1], oldAi=body.inventory[0], nativeCursor=Main.mouseItem;
            bool opaque=Main.SettingsEnabled_OpaqueBoxBehindTooltips; byte color=Main.mouseTextColor;
            var border=Main.MouseBorderColor;
            int npcIndex=Main.instance.mouseNPCIndex, npcType=Main.instance.mouseNPCType;
            var favoriteKey=Main.FavoriteKey;
            var oldDye=body.dye[0]; var savedStoredDye=body.Loadouts[1].Dye[0];
            try
            {
                graphics.LoadItemTextures(new[]{(int)ItemID.DirtBlock, ItemID.WoodenBow, ItemID.Shellphone, ItemID.ShellphoneOcean, ItemID.RedDye});
                TextureAssets.InventoryBack13=Main.Assets.Request<Texture2D>("Images/Inventory_Back13",AssetRequestMode.ImmediateLoad);
                TextureAssets.Cursors[0]=Main.Assets.Request<Texture2D>("Images/UI/Cursor_0",AssetRequestMode.ImmediateLoad);
                TextureAssets.InventoryBack10=Main.Assets.Request<Texture2D>("Images/Inventory_Back10",AssetRequestMode.ImmediateLoad);
                for(int i=310;i<=315;i++)
                    TextureAssets.Extra[i]=Main.Assets.Request<Texture2D>("Images/Extra_"+i,AssetRequestMode.ImmediateLoad);
                Main.MouseBorderColor=Color.Transparent; Main.mouseTextColor=255;
                // ProbeGraphics constructs Main without a game constructor.
                // Its default 0 is the housing NPC cursor and suppresses even
                // a vanilla backpack tooltip. The live game's idle value is -1.
                Main.instance.SetMouseNPC(-1,-1);
                EquipmentSlots(context, window, host, human, body, graphics, output, matrix, screen);
                human.inventory[0]=new Item(ItemID.DirtBlock) { stack=20 }; human.inventory[1]=new Item();
                body.inventory[0]=new Item(ItemID.DirtBlock) { stack=10 };
                Call(window,"Prepare",matrix,screen);
                Vector2 p=NativeToolsUiChecks.Point(Get(Slot(window,true,0),"Rect"))*matrix.M11;
                Vector2 ai=NativeToolsUiChecks.Point(Get(Slot(window,false,0),"Rect"))*matrix.M11;
                Vector2 dest=NativeToolsUiChecks.Point(Get(Slot(window,true,1),"Rect"))*matrix.M11;
                Frame(context,p,false); Frame(context,p,true); Frame(context,p,false); Frame(context,p,true); Frame(context,p,false);
                Require(((Item)Get(host,"CursorItem")).stack==2,"two distinct physical right clicks accumulate two items");
                Frame(context,ai,true);
                for(int i=0;i<34;i++) Frame(context,ai,true);
                Frame(context,ai,false);
                int picked=((Item)Get(host,"CursorItem")).stack;
                Require(picked>3 && human.inventory[0].stack==20 && body.inventory[0].stack==10 && ReferenceEquals(Main.mouseItem,nativeCursor),
                    "a physical hold on the other body's compatible stack accumulates only on the projected cursor");
                Frame(context,dest,true); Frame(context,dest,false); Require(((Item)Get(host,"CursorItem")).stack==picked,"right does not deposit into an empty slot");
                Frame(context,dest,false,true); Frame(context,dest,false);
                Require(human.inventory[0].stack==18 && human.inventory[1].stack==picked && body.inventory[0].stack==12-picked &&
                    !(bool)Get(host,"HasItemGesture"), "left places the sum from both native inventories once");
                Main.FavoriteKey=Keys.RightControl;
                Frame(context,ai,false,true,Keys.RightControl);Frame(context,ai,false);
                Require(body.inventory[0].favorited && (bool)Get(Slot(window,false,0),"Favorited") && !human.inventory[0].favorited &&
                    !(bool)Get(host,"HasItemGesture"),"a remapped native favorite key marks only the pointed AI inventory, taking precedence over quick trash");
                body.dye[0]=new Item(ItemID.RedDye);
                body.Loadouts[1].Dye[0]=new Item(ItemID.RedDye) { favorited=true };
                Call(window,"Prepare",matrix,screen);
                var dyePoint=NativeToolsUiChecks.Point(Get(Slot(window,false,0,"Dye"),"Rect"))*matrix.M11;
                Frame(context,dyePoint,false); Frame(context,dyePoint,false,true,Keys.RightControl); Frame(context,dyePoint,false);
                Require(body.dye[0].favorited && !body.Loadouts[1].Dye[0].favorited &&
                    (int)Get(Slot(window,false,0,"Dye"),"LoadoutIcon")==310+body.CurrentLoadoutIndex && !(bool)Get(Slot(window,false,0,"Dye"),"Favorited"),
                    "remapped key on dye enables native loadout sharing with its distinct stock marker");
                graphics.Image(Path.Combine(output,"companion-native-slot-marks.png"),()=>Call(window,"Draw"),matrix,(int)screen.X,(int)screen.Y);
                body.dye[0]=new Item();body.Loadouts[1].Dye[0].favorited=true;Call(window,"Prepare",matrix,screen);
                Require((int)Get(Slot(window,false,0,"Dye"),"LoadoutIcon")==314 && (int)Get(Slot(window,false,0,"Dye"),"ItemType")==ItemID.RedDye,
                    "an empty active dye slot displays the correct other loadout's actual shared item");
                body.inventory[0]=new Item(ItemID.Shellphone); Call(window,"Prepare",matrix,screen);
                Frame(context,ai,true);
                for(int i=0;i<35;i++) Frame(context,ai,true);
                Frame(context,ai,false);
                Require(body.inventory[0].type==ItemID.ShellphoneOcean && !(bool)Get(host,"HasItemGesture") &&
                    !PlayerInput.Triggers.Current.MouseRight, "the actual window switches the phone only on its new right edge");

                body.inventory[0]=new Item(ItemID.WoodenBow); body.inventory[0].Prefix(PrefixID.Demonic);
                Call(window,"Prepare",matrix,screen); Frame(context,ai,false);
                Require(GetOptional(Slot(window,false,0),"Hint")==null,"an occupied slot has no competing custom hint");
                var clear=AccessTools.Method(typeof(Main),"MouseOversClear");
                var pending=AccessTools.Method(typeof(Main),"DrawPendingMouseText");
                void NativeTooltip()
                {
                    clear.Invoke(null,null);
                    ItemSlot.MouseHover(body.inventory,ItemSlot.Context.InventoryItem,0);
                    int x=Main.SettingsEnabled_OpaqueBoxBehindTooltips?Main.mouseX+6:-1;
                    int y=Main.SettingsEnabled_OpaqueBoxBehindTooltips?Main.mouseY+6:-1;
                    Main.instance.MouseText(Main.hoverItemName,0,0,x,y);
                    pending.Invoke(null,null);
                }
                void PanelTooltip(bool drawPanel)
                {
                    clear.Invoke(null,null); Call(shell,"BeginPointerLayer");
                    // An overlapping lower native inventory may replace HoverItem
                    // after our reservation. The real final layer must recover it.
                    ItemSlot.MouseHover(human.inventory,ItemSlot.Context.InventoryItem,0);
                    if(drawPanel) Call(shell,"DrawLayer");
                    Call(shell,"HoverGateLayer"); Call(shell,"EndPointerLayer");
                    Require(Main.HoverItem.type==ItemID.WoodenBow && Main.HoverItem.prefix==PrefixID.Demonic &&
                        Main.HoverItem.tooltipContext==ItemSlot.Context.InventoryItem && Main.HoverItem.tooltipSlot==0,
                        "the final native tooltip has this exact actor item, prefix and slot");
                    pending.Invoke(null,null);
                }
                foreach(bool background in new[]{true,false})
                {
                    Main.SettingsEnabled_OpaqueBoxBehindTooltips=background;
                    var native=graphics.Pixels(NativeTooltip,matrix,(int)screen.X,(int)screen.Y);
                    var actual=graphics.Pixels(()=>PanelTooltip(false),matrix,(int)screen.X,(int)screen.Y);
                    int referencePixels=native.Count(pixel=>pixel.A!=0), actualPixels=actual.Count(pixel=>pixel.A!=0);
                    Require(referencePixels>500 && actual.SequenceEqual(native),
                        "the full genuine item-tooltip pixels equal the vanilla inventory at the same pointer, opaque="+background+
                        " reference="+referencePixels+" actual="+actualPixels+" differences="+native.Where((pixel,i)=>pixel!=actual[i]).Count()+
                        " pointer="+Main.mouseX+","+Main.mouseY+" original="+ai);
                }
                Main.SettingsEnabled_OpaqueBoxBehindTooltips=true;
                graphics.Image(Path.Combine(output,"companion-native-slot-tooltip.png"),()=>PanelTooltip(true),matrix,(int)screen.X,(int)screen.Y);
                Console.WriteLine("PASS actual repeated/held right input, shellphone edge, configured favorite key, inventory and loadout marks, inherited dye, native opaque/transparent tooltip pixels and real hovered item identity.");
            }
            finally
            {
                Call(host,"CancelItemGesture");human.inventory[0]=oldOwner;human.inventory[1]=oldTarget;body.inventory[0]=oldAi;
                Main.SettingsEnabled_OpaqueBoxBehindTooltips=opaque;Main.mouseTextColor=color;Main.MouseBorderColor=border;
                Main.instance.SetMouseNPC(npcIndex,npcType);
                Main.FavoriteKey=favoriteKey;body.dye[0]=oldDye;body.Loadouts[1].Dye[0]=savedStoredDye;
                Frame(context,Vector2.Zero,false);Main.ClearHoverItem();
            }
        }

        private static void EquipmentSlots(object context, object window, object host, Player human, Player body,
            ProbeGraphics graphics, string output, Matrix matrix, Vector2 screen)
        {
            // Terraria 1.4.6.1 ItemSlot.Draw, Extra[54] for contexts 19, 20, 18, 17 and 16.
            int[] types = { ItemID.Carrot, ItemID.MagicLantern, ItemID.Minecart, ItemID.SlimySaddle, ItemID.GrapplingHook };
            int[] icons = { 10, 17, 7, 13, 4 };
            int[] dyeSlots = { 2, 4 };
            var ownerGear = Get(window, "ownerGearPage");
            var companionGear = Get(window, "companionGearPage");
            var players = new[] { human, body };
            var previousItems = players.Select(player => player.miscEquips.ToArray()).ToArray();
            var previousDyes = players.Select(player => player.miscDyes.ToArray()).ToArray();
            var previousBag = players.Select(player => player.inventory[2]).ToArray();
            NativeCompanionItemGestureChecks.InitializeNativeEquipment();
            graphics.LoadItemTextures(types.Concat(new[] { (int)ItemID.RedDye, ItemID.BlueDye }).ToArray());

            Vector2 Point(object control) => NativeToolsUiChecks.Point(Get(control, "Rect")) * matrix.M11;
            void Click(object control)
            {
                var pointer = Point(control);
                Frame(context, pointer, false, true);
                Frame(context, pointer, false);
            }
            void CheckCell(bool owner, string area, int slot, int type, int frame)
            {
                var cell = Slot(window, owner, slot, area);
                int actualType = (int)Get(cell, "ItemType"), actualFrame = (int)Get(cell, "EmptyIcon");
                Require(actualType == type && actualFrame == frame,
                    $"{(owner ? "owner" : "companion")} {area} {slot}: item {actualType}, native empty frame {actualFrame}, expected item {type}, frame {frame}");
            }

            try
            {
                Set(window, "ownerGearPage", 2);
                Set(window, "companionGearPage", 2);
                Call(window, "Invalidate");
                foreach (var player in players)
                    for (int slot = 0; slot < icons.Length; slot++)
                    {
                        player.miscEquips[slot] = new Item();
                        player.miscDyes[slot] = new Item();
                    }
                Call(window, "Prepare", matrix, screen);
                foreach (bool owner in new[] { true, false })
                    for (int slot = 0; slot < icons.Length; slot++) CheckCell(owner, "Misc", slot, 0, icons[slot]);
                graphics.Image(Path.Combine(output, "companion-native-misc-empty-slots.png"),
                    () => Call(window, "Draw"), matrix, (int)screen.X, (int)screen.Y);

                for (int side = 0; side < players.Length; side++)
                {
                    bool owner = side == 0;
                    var player = players[side];
                    for (int slot = 0; slot < types.Length; slot++)
                    {
                        int wrongSlot = (slot + 1) % types.Length;
                        player.inventory[2] = new Item(types[slot]);
                        Call(window, "Prepare", matrix, screen);
                        Click(Slot(window, owner, 2));
                        Click(Slot(window, owner, wrongSlot, "Misc"));
                        Require(((Item)Get(host, "CursorItem")).type == types[slot] && player.miscEquips[wrongSlot].IsAir,
                            "physical equipment deposit rejects a different slot category and retains the held item");
                        CheckCell(owner, "Misc", wrongSlot, 0, icons[wrongSlot]);
                        Click(Slot(window, owner, slot, "Misc"));
                        Require(player.inventory[2].IsAir && player.miscEquips[slot].type == types[slot] && !(bool)Get(host, "HasItemGesture"),
                            $"physical deposit writes {(owner ? "owner" : "companion")} Misc {slot}: bag {player.inventory[2].type}, gear {player.miscEquips[slot].type}, expected {types[slot]}, feedback {GetOptional(window, "feedback")}");
                        CheckCell(owner, "Misc", slot, types[slot], -1);
                        Click(Slot(window, owner, slot, "Misc"));
                        Click(Slot(window, owner, 2));
                        Require(player.miscEquips[slot].IsAir && player.inventory[2].type == types[slot] && !(bool)Get(host, "HasItemGesture"),
                            "physical withdrawal restores the item to the original bag slot");
                        CheckCell(owner, "Misc", slot, 0, icons[slot]);
                    }

                    foreach (int slot in dyeSlots)
                    {
                        int type = slot == 2 ? ItemID.RedDye : ItemID.BlueDye;
                        player.inventory[2] = new Item(type);
                        Call(window, "Prepare", matrix, screen);
                        CheckCell(owner, "MiscDye", slot, 0, 1);
                        var equipment = Point(Slot(window, owner, slot, "Misc"));
                        var dye = Point(Slot(window, owner, slot, "MiscDye"));
                        Require(dye.X == equipment.X && dye.Y > equipment.Y, "misc dye directly below the matching equipment slot");
                        Click(Slot(window, owner, 2));
                        Click(Slot(window, owner, slot, "MiscDye"));
                        Require(player.miscDyes[slot].type == type && player.miscEquips[slot].IsAir && !(bool)Get(host, "HasItemGesture"),
                            "physical dye deposit writes the matching native dye index");
                        CheckCell(owner, "MiscDye", slot, type, -1);
                    }
                    Require(player.miscDyes[2].type == ItemID.RedDye && player.miscDyes[4].type == ItemID.BlueDye,
                        "minecart and hook dye keep distinct indices");
                }
                Console.WriteLine("PASS both equipment columns: five exact vanilla empty frames, five accepted and rejected physical slot transfers and withdrawals, and distinct minecart/hook dye pairing.");
            }
            finally
            {
                Call(host, "CancelItemGesture");
                for (int side = 0; side < players.Length; side++)
                {
                    Array.Copy(previousItems[side], players[side].miscEquips, previousItems[side].Length);
                    Array.Copy(previousDyes[side], players[side].miscDyes, previousDyes[side].Length);
                    players[side].inventory[2] = previousBag[side];
                }
                Set(window, "ownerGearPage", ownerGear);
                Set(window, "companionGearPage", companionGear);
                Call(window, "Invalidate");
                Call(window, "Prepare", matrix, screen);
            }
        }
    }
}
