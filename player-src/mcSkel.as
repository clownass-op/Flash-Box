package
{
   import flash.display.MovieClip;
   import flash.geom.Point;
   
   [Embed(source="/_assets/assets.swf", symbol="symbol300")]
   public class mcSkel extends MovieClip
   {
      
      public var arrow:MovieClip;
      
      public var backfoot:MovieClip;
      
      public var backhair:MovieClip;
      
      public var backhand:MovieClip;
      
      public var backrobe:MovieClip;
      
      public var backshin:MovieClip;
      
      public var backshoulder:MovieClip;
      
      public var backthigh:MovieClip;
      
      public var cape:MovieClip;
      
      public var chest:MovieClip;
      
      public var emoteFX:MovieClip;
      
      public var frontfoot:MovieClip;
      
      public var fronthand:MovieClip;
      
      public var frontshin:MovieClip;
      
      public var frontshoulder:MovieClip;
      
      public var frontthigh:MovieClip;
      
      public var head:MovieClip;
      
      public var hip:MovieClip;
      
      public var hitbox:MovieClip;
      
      public var idlefoot:MovieClip;
      
      public var pvpFlag:MovieClip;
      
      public var robe:MovieClip;
      
      public var shield:MovieClip;
      
      public var weapon:MovieClip;
      
      public var weaponFist:MovieClip;
      
      public var weaponFistOff:MovieClip;
      
      public var weaponOff:MovieClip;
      
      public var weaponTemp:MovieClip;
      
      public var animLoop:int;
      
      public var avtMC:MovieClip;
      
      public var projClass:Class;
      
      public var projMC:MovieClip;
      
      public var sp:Point;
      
      public var ep:Point;
      
      public var onMove:Boolean = false;
      
      public var hideFeet:Boolean;
      
      public function mcSkel()
      {
         super();
         addFrameScript(0,this.frame1,7,this.frame8,8,this.frame9,16,this.frame17,20,this.frame21,27,this.frame28,32,this.frame33,40,this.frame41,45,this.frame46,53,this.frame54,67,this.frame68,68,this.frame69,71,this.frame72,84,this.frame85,85,this.frame86,92,this.frame93,98,this.frame99,99,this.frame100,116,this.frame117,117,this.frame118,130,this.frame131,131,this.frame132,155,this.frame156,165,this.frame166,166,this.frame167,185,this.frame186,186,this.frame187,200,this.frame201,209,this.frame210,210,this.frame211,244,this.frame245,245,this.frame246,249,this.frame250,261,this.frame262,262,this.frame263,271,this.frame272,280,this.frame281,288,this.frame289,289,this.frame290,309,this.frame310,312,this.frame313,313,this.frame314,345,this.frame346,346,this.frame347,364,this.frame365,366,this.frame367,367,this.frame368,372,this.frame373,392,this.frame393,393,this.frame394,457,this.frame458,458,this.frame459,475,this.frame476,494,this.frame495,502,this.frame503,510,this.frame511,511,this.frame512,512
         ,this.frame513,558,this.frame559,559,this.frame560,589,this.frame590,590,this.frame591,598,this.frame599,599,this.frame600,607,this.frame608,620,this.frame621,621,this.frame622,632,this.frame633,643,this.frame644,653,this.frame654,659,this.frame660,677,this.frame678,695,this.frame696,702,this.frame703,705,this.frame706,721,this.frame722,722,this.frame723,725,this.frame726,751,this.frame752,752,this.frame753,756,this.frame757,780,this.frame781,781,this.frame782,785,this.frame786,808,this.frame809,809,this.frame810,826,this.frame827,827,this.frame828,848,this.frame849,849,this.frame850,855,this.frame856,856,this.frame857,885,this.frame886,886,this.frame887,909,this.frame910,910,this.frame911,913,this.frame914,930,this.frame931,931,this.frame932,934,this.frame935,957,this.frame958,958,this.frame959,961,this.frame962,983,this.frame984,984,this.frame985,987,this.frame988,1001,this.frame1002,1002,this.frame1003,1013,this.frame1014,1014,this.frame1015,1017,this.frame1018,1033,this.frame1034,1034
         ,this.frame1035,1037,this.frame1038,1048,this.frame1049,1049,this.frame1050,1070,this.frame1071,1071,this.frame1072,1082,this.frame1083,1083,this.frame1084,1087,this.frame1088,1096,this.frame1097,1097,this.frame1098,1100,this.frame1101,1111,this.frame1112,1112,this.frame1113,1121,this.frame1122,1122,this.frame1123,1126,this.frame1127,1135,this.frame1136,1136,this.frame1137,1139,this.frame1140,1150,this.frame1151,1151,this.frame1152,1162,this.frame1163,1163,this.frame1164,1166,this.frame1167,1178,this.frame1179,1179,this.frame1180,1182,this.frame1183,1191,this.frame1192,1192,this.frame1193,1203,this.frame1204,1204,this.frame1205,1206,this.frame1207,1207,this.frame1208,1221,this.frame1222,1222,this.frame1223,1226,this.frame1227,1243,this.frame1244,1247,this.frame1248,1253,this.frame1254,1254,this.frame1255,1257,this.frame1258,1266,this.frame1267,1267,this.frame1268,1269,this.frame1270,1283,this.frame1284,1284,this.frame1285,1295,this.frame1296,1296,this.frame1297,1297,this.frame1298,1307,this
         .frame1308,1319,this.frame1320,1320,this.frame1321,1339,this.frame1340,1340,this.frame1341,1354,this.frame1355,1355,this.frame1356,1370,this.frame1371,1371,this.frame1372,1404,this.frame1405,1405,this.frame1406,1442,this.frame1443,1443,this.frame1444,1451,this.frame1452,1452,this.frame1453,1524,this.frame1525,1525,this.frame1526,1562,this.frame1563,1563,this.frame1564,1578,this.frame1579,1579,this.frame1580,1588,this.frame1589,1589,this.frame1590,1620,this.frame1621,1621,this.frame1622,1624,this.frame1625,1647,this.frame1648,1648,this.frame1649,1651,this.frame1652,1673,this.frame1674,1674,this.frame1675,1690,this.frame1691,1691,this.frame1692,1704,this.frame1705,1705,this.frame1706,1724,this.frame1725,1725,this.frame1726,1766,this.frame1767,1767,this.frame1768,1769,this.frame1770,1781,this.frame1782,1782,this.frame1783,1794,this.frame1795,1795,this.frame1796,1800,this.frame1801,1818,this.frame1819,1819,this.frame1820,1821,this.frame1822,1836,this.frame1837,1837,this.frame1838,1853,this.frame1854
         ,1856,this.frame1857,1857,this.frame1858,1860,this.frame1861,1866,this.frame1867,1867,this.frame1868,1887,this.frame1888);
      }
      
      public function emoteLoopFrame() : int
      {
         var _loc1_:int = 0;
         while(_loc1_ < currentLabels.length)
         {
            if(currentLabels[_loc1_].name == currentLabel)
            {
               return currentLabels[_loc1_].frame;
            }
            _loc1_++;
         }
         return 8;
      }
      
      public function emoteLoop(param1:int, param2:Boolean = true) : void
      {
         var _loc3_:int = this.emoteLoopFrame();
         if(_loc3_ > 8)
         {
            if(++this.animLoop < param1)
            {
               this.gotoAndPlay(_loc3_ + 1);
               return;
            }
         }
         if(param2)
         {
            this.gotoAndPlay("Idle");
         }
      }
      
      // Which foot the current animation wants; kept even while the armor
      // is hidden (hideFeet) so unhiding restores the right one.
      public var frontFootShown:Boolean = false;
      
      public function showIdleFoot() : *
      {
         this.frontFootShown = false;
         this.frontfoot.visible = false;
         this.idlefoot.visible = !this.hideFeet;
      }
      
      public function showFrontFoot() : *
      {
         this.frontFootShown = true;
         this.idlefoot.visible = false;
         this.frontfoot.visible = !this.hideFeet;
      }
      
      // Capes without Idle/Move labels (static art) used to throw here and
      // abort the rest of the frame script (e.g. Idle's stop()).
      public function capeGoto(param1:String, param2:Boolean) : void
      {
         var _loc3_:MovieClip = null;
         var _loc4_:Object = null;
         try
         {
            _loc3_ = this.cape.cape as MovieClip;
            if(_loc3_ == null)
            {
               return;
            }
            for each(_loc4_ in _loc3_.currentLabels)
            {
               if(_loc4_.name == param1)
               {
                  if(param2)
                  {
                     _loc3_.gotoAndPlay(param1);
                  }
                  else
                  {
                     _loc3_.gotoAndStop(param1);
                  }
                  return;
               }
            }
         }
         catch(e:*)
         {
         }
      }
      
      override public function gotoAndPlay(param1:Object, param2:String = null) : void
      {
         var _loc3_:Array = null;
         if(MovieClip(parent).pAV != null && MovieClip(parent).pAV.morphMC != null && Boolean(MovieClip(parent).pAV.morphMC.visible))
         {
            _loc3_ = ["Idle","Walk"];
            if(_loc3_.indexOf(param1) > -1)
            {
               if(MovieClip(parent).pAV.morphMC.currentLabel != param1)
               {
                  MovieClip(parent).pAV.morphMC.onMove = true;
                  MovieClip(parent).pAV.morphMC.gotoAndPlay(param1);
               }
            }
         }
         if(!this.handleAnimEvent(String(param1)))
         {
            super.gotoAndPlay(param1);
         }
      }
      
      private function handleAnimEvent(param1:String) : Boolean
      {
         var f:Function = null;
         var o:Boolean = false;
         var strAnim:String = param1;
         var animEvents:Object = MovieClip(parent).AnimEvent;
         if(animEvents[strAnim] == null)
         {
            return false;
         }
         f = animEvents[strAnim][0];
         o = Boolean(animEvents[strAnim][1]);
         try
         {
            f();
         }
         catch(e:*)
         {
            trace("[Avatar] ERR handleAnimEvent: armor missing anim listener " + strAnim);
         }
         if(!o || strAnim == "Idle")
         {
            return false;
         }
         return true;
      }
      
      internal function frame1() : *
      {
         this.animLoop = 0;
         this.avtMC = null;
         this.gotoAndPlay("Idle");
      }
      
      internal function frame8() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
         stop();
      }
      
      internal function frame9() : *
      {
         gotoAndStop("Idle");
      }
      
      internal function frame17() : *
      {
         this.showFrontFoot();
         this.capeGoto("Move",true);
      }
      
      internal function frame21() : *
      {
         if(this.onMove)
         {
            this.gotoAndPlay("mountWalk");
         }
      }
      
      internal function frame28() : *
      {
         this.showFrontFoot();
         this.capeGoto("Move",true);
      }
      
      internal function frame33() : *
      {
         if(this.onMove)
         {
            this.gotoAndPlay("horseWalk");
         }
      }
      
      internal function frame41() : *
      {
         this.showFrontFoot();
         this.capeGoto("Move",true);
      }
      
      internal function frame46() : *
      {
         if(this.onMove)
         {
            this.gotoAndPlay("throneWalk");
         }
      }
      
      internal function frame54() : *
      {
         this.showFrontFoot();
         this.capeGoto("Move",true);
      }
      
      internal function frame68() : *
      {
         if(this.onMove)
         {
            this.gotoAndPlay("Walk");
         }
      }
      
      internal function frame69() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame72() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            if(this.avtMC.spFX.strl != null && this.avtMC.spFX.strl != "" && this.avtMC.spFX.avts != null)
            {
               MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
               this.avtMC.spellDur = 0;
            }
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame85() : *
      {
         this.gotoAndPlay("Dance");
      }
      
      internal function frame86() : *
      {
         this.animLoop = 0;
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame93() : *
      {
         this.emoteLoop(3,false);
      }
      
      internal function frame99() : *
      {
         stop();
      }
      
      internal function frame100() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame117() : *
      {
         stop();
      }
      
      internal function frame118() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame131() : *
      {
         this.gotoAndPlay("Use");
      }
      
      internal function frame132() : *
      {
         this.animLoop = 0;
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame156() : *
      {
         this.emoteLoop(3,false);
      }
      
      internal function frame166() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame167() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame186() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame187() : *
      {
         this.animLoop = 0;
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame201() : *
      {
         this.emoteLoop(3,false);
      }
      
      internal function frame210() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame211() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame245() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame246() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame250() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            if(this.avtMC.spFX.strl != null && this.avtMC.spFX.strl != "" && this.avtMC.spFX.avts != null)
            {
               MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
               this.avtMC.spellDur = 0;
            }
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame262() : *
      {
         this.gotoAndPlay("Airguitar");
      }
      
      internal function frame263() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame272() : *
      {
         this.showFrontFoot();
      }
      
      internal function frame281() : *
      {
         this.showIdleFoot();
      }
      
      internal function frame289() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame290() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame310() : *
      {
         if(this.scaleX < 0)
         {
            this.emoteFX.scaleX *= -1;
         }
      }
      
      internal function frame313() : *
      {
         stop();
      }
      
      internal function frame314() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame346() : *
      {
         if(this.onMove)
         {
            this.gotoAndPlay("Walk");
         }
         stop();
      }
      
      internal function frame347() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame365() : *
      {
         this.showIdleFoot();
      }
      
      internal function frame367() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame368() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame373() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            if(this.avtMC.spFX.strl != null && this.avtMC.spFX.strl != "" && this.avtMC.spFX.avts != null)
            {
               MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
               this.avtMC.spellDur = 0;
            }
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame393() : *
      {
         this.gotoAndPlay("Dance2");
      }
      
      internal function frame394() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame458() : *
      {
         this.gotoAndPlay("Swordplay");
      }
      
      internal function frame459() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame476() : *
      {
         this.showFrontFoot();
      }
      
      internal function frame495() : *
      {
         stop();
      }
      
      internal function frame503() : *
      {
         this.animLoop = 0;
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame511() : *
      {
         this.emoteLoop(3);
      }
      
      internal function frame512() : *
      {
         stop();
      }
      
      internal function frame513() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame559() : *
      {
         stop();
      }
      
      internal function frame560() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame590() : *
      {
         stop();
      }
      
      internal function frame591() : *
      {
         this.animLoop = 0;
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame599() : *
      {
         this.emoteLoop(3);
      }
      
      internal function frame600() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame608() : *
      {
         this.weapon.visible = true;
      }
      
      internal function frame621() : *
      {
         stop();
      }
      
      internal function frame622() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame633() : *
      {
         stop();
      }
      
      internal function frame644() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame654() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame660() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame678() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame696() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame703() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame706() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame722() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame723() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame726() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame752() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame753() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame757() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame781() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame782() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame786() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame809() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame810() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame827() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame828() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame849() : *
      {
         stop();
      }
      
      internal function frame850() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame856() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame857() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame886() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame887() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame910() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame911() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame914() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame931() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame932() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame935() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame958() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame959() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame962() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame984() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame985() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame988() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1002() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1003() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1014() : *
      {
         stop();
      }
      
      internal function frame1015() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1018() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1034() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1035() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1038() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1049() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1050() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1071() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1072() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1083() : *
      {
         stop();
      }
      
      internal function frame1084() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1088() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1097() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1098() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1101() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1112() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1113() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1122() : *
      {
         stop();
      }
      
      internal function frame1123() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1127() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1136() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1137() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1140() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1151() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1152() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1163() : *
      {
         stop();
      }
      
      internal function frame1164() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1167() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1179() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1180() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1183() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1192() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1193() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1204() : *
      {
         stop();
      }
      
      internal function frame1205() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
         this.avtMC = MovieClip(parent);
      }
      
      internal function frame1207() : *
      {
         try
         {
            if(this.avtMC.spFX.strl == null || this.avtMC.spFX.strl == "" || this.avtMC.spFX.avts == null)
            {
               trace("creating projectile");
               if(this.avtMC.pAV.strProj != "")
               {
                  trace("strProj:" + this.avtMC.pAV.strProj + ":end");
                  try
                  {
                     this.projClass = this.avtMC.pAV.rootClass.world.getClass(this.avtMC.pAV.strProj) as Class;
                  }
                  catch(e:*)
                  {
                  }
               }
               trace("projClass: " + this.projClass);
               if(this.projClass == null)
               {
                  this.projClass = this.avtMC.pAV.rootClass.world.getClass("p_ar") as Class;
               }
               if(this.projClass != null)
               {
                  this.projMC = new this.projClass() as MovieClip;
                  this.projMC.scaleX *= 0.3;
                  this.projMC.scaleY *= 0.3;
                  this.sp = this.backhand.localToGlobal(new Point(this.backhand.x,this.backhand.y));
                  this.projMC.x = this.sp.x;
                  this.projMC.y = this.sp.y;
                  this.ep = new Point(this.avtMC.pAV.target.pMC.x,this.avtMC.pAV.target.pMC.y);
                  this.ep.y -= this.avtMC.pAV.target.pMC.height / 3.5;
                  this.avtMC.pAV.rootClass.world.addChild(this.projMC);
                  this.avtMC.projClass = new Projectile(this.sp,this.ep,this.projMC,this.avtMC.pAV.rootClass);
                  this.projClass = null;
               }
            }
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1208() : *
      {
         try
         {
            if(this.avtMC.spFX.strl != null && this.avtMC.spFX.strl != "" && this.avtMC.spFX.avts != null)
            {
               MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null);
            }
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1222() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1223() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
         this.avtMC = MovieClip(parent);
      }
      
      internal function frame1227() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1244() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1248() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1254() : *
      {
         stop();
      }
      
      internal function frame1255() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1258() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1267() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1268() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1270() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1284() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1285() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1296() : *
      {
         this.showIdleFoot();
      }
      
      internal function frame1297() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1298() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1308() : *
      {
         this.showIdleFoot();
      }
      
      internal function frame1320() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1321() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1340() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1341() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1355() : *
      {
         stop();
      }
      
      internal function frame1356() : *
      {
         this.showFrontFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1371() : *
      {
         stop();
      }
      
      internal function frame1372() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1405() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1406() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1443() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1444() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1452() : *
      {
         this.gotoAndPlay("Cry2");
      }
      
      internal function frame1453() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1525() : *
      {
         this.gotoAndPlay("Spar");
      }
      
      internal function frame1526() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1563() : *
      {
         this.gotoAndPlay("Samba");
      }
      
      internal function frame1564() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1579() : *
      {
         this.gotoAndPlay("Stepdance");
      }
      
      internal function frame1580() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1589() : *
      {
         this.gotoAndPlay("Headbang");
      }
      
      internal function frame1590() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1621() : *
      {
         this.gotoAndPlay("Dazed");
      }
      
      internal function frame1622() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1625() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null);
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1648() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1649() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1652() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null);
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1674() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1675() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1691() : *
      {
         this.gotoAndPlay("Danceweapon");
      }
      
      internal function frame1692() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1705() : *
      {
         this.gotoAndPlay("Useweapon");
      }
      
      internal function frame1706() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1725() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1726() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1767() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1768() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1770() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1782() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1783() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1795() : *
      {
         stop();
      }
      
      internal function frame1796() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1801() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1819() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1820() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
         this.avtMC = MovieClip(parent);
      }
      
      internal function frame1822() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1837() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1838() : *
      {
         try
         {
            this.showIdleFoot();
            this.capeGoto("Idle",false);
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1854() : *
      {
         this.weapon.visible = true;
      }
      
      internal function frame1857() : *
      {
         this.gotoAndPlay("Fight");
      }
      
      internal function frame1858() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1861() : *
      {
         try
         {
            this.avtMC = MovieClip(parent);
            MovieClip(this.avtMC.parent.parent).castSpellFX(this.avtMC.pAV,this.avtMC.spFX,null,this.avtMC.spellDur);
            this.avtMC.spellDur = 0;
         }
         catch(e:*)
         {
         }
      }
      
      internal function frame1867() : *
      {
         MovieClip(parent).endAction();
      }
      
      internal function frame1868() : *
      {
         this.showIdleFoot();
         this.capeGoto("Idle",false);
      }
      
      internal function frame1888() : *
      {
         MovieClip(parent).endAction();
      }
   }
}

