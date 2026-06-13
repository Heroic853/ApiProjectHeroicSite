using Client.Service;
using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http.Json;


namespace Client.Pages
{
    public partial class Aniversary
    {
        private bool showModal = false;
        private int currentIndex = 0;
        private MonsterEntry currentMonster => monsters[currentIndex];

        private void OpenModal(int index) { currentIndex = index; showModal = true; }
        private void CloseModal() { showModal = false; }
        private void NextMonster() { currentIndex = (currentIndex + 1) % monsters.Count; }
        private void PrevMonster() { currentIndex = (currentIndex - 1 + monsters.Count) % monsters.Count; }

        private record MonsterEntry(string Name, string ThumbnailPath, string LargeImagePath, string? NexusLink, string Description = "[DESCRIPTION]");

        private List<MonsterEntry> monsters = new()
        {
            new("Destroid Tigrex", "images/4th.webp", "images/DestroidTigrexRender.webp" , "https://www.nexusmods.com/monsterhunterworld/mods/5827", 
                "Destroier tigrex was ifluenced by rathalos Destructor wivern to kill and eat all monster " +
                "wich unknown powers. Now this tigrex have explosion roars of Xeno and can invade all maps wich " +
                "Xeno stones wuch mental phihic magic."
            ),
            
            new("God Alatreon","images/5th.webp", "images/GodAlatreonRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5436", 
                "The God Alatreon is hier wich golden scale and terief eyes"
            ),
            
            new("Shara Ishava Demon", "images/6th.webp", "images/SharaDemonRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5635", 
                "He new monster shara died and this soul come in hell " +
                "and become a demon from Dante demon."
            ),
            
            new("Destruction Rathalos", "images/7th.webp", "images/DestroidRathRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5819", 
                "The distruction of wiverns come to kill all wyvers wich blue flame and mental control of earth. " +
                "The hunter now need to kill the DESTROIER WYVER RATHALOS EXTREME!! " +
                "Who get allive?"
            ),
            
            new("Blinking Nargacuga", "images/8th.webp", "images/BlinkingRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/6285", 
                "Blinking Nargacuga The new world A light moved in the forest.  A white beast with red spines was moving at " +
                "great speed.  A hunter who studied monsters analyzed the creature but it looked like a white demon moving. " +
                "The book found by the football player described the monster as a white nargacuga with red eyes " +
                "that came from another world.  The other information was now covered in blood and instead the hunter " +
                "was dead and stuffed with white and blood-painted feathers. The commander when you discover " +
                "this and other anomalies with the sighting of unknown monsters I call powerful hunters of the guild " +
                "where for them the Destroid Tigrex was nothing and they had also survived the invasion of " +
                "the monsters of MHXR.  When we reached the ancient forest, the night was thick and from a " +
                "distance a hunter saw that a portal had just opened.  That portal connected the new Frontier " +
                "universe where monsters will come to conquer the world.  These hunters will now go hunting to face " +
                "this unknown beast but only with you will they succeed!"
            ),
            
            new("UNKNOWN", "images/9th.webp", "images/UncknownRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5817", 
                "True Unknown come in this world to destroid the world wich him soltrice wars of furies. " +
                "He now have attack of hell and super rage and scared attacks. " +
                "Fire hell attack and changesof sky wich fire."
            ),
            
            new("Primordial Risen Alatreon", "images/10th.webp", "images/AlatreonRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/6981", 
                "After the battle of Armageddon in the world of Icerbone a healer rises underground " +
                "and was the last survivor of the ancient war and thus showed himself Primordial Alatreon " +
                "the first pure Alatreon with blue wings, silver scales as if he had just been born like " +
                "the Kushala Doara, with golden quills and eyes as blue as the sky. As soon as he was resurrected " +
                "with his wounds he was like a healer just born to fight. The hatred for the " +
                "hunters was enormous and for this reason he wanted to take revenge as the only albino " +
                "Alatreon and destroy everyone with his true resurrected powers."
            ),
            
            new("Configuration Glavenus", "images/11thgl.webp", "images/GlavusConfigRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5202", 
                "New monster caled Configuration Hellblade Glavenus, a monster come from hell wich " +
                "fury and rage replace the normal glavenus"
            ),
            
            new("Zenith Gore Magala", "images/12th.webp", "images/ZenithGore.webp", "https://www.nexusmods.com/monsterhunterrise/mods/2410", 
                "After the encounter of Zenith Shagaru Magala in the darkness and dark core a cocoon was growing. " +
                "A dark soul enveloped in the darkest darkness that was storing all of her power.  " +
                "After a couple of years of meeting the Shagaru Magala the beast awoke.  It looked like the awakening of " +
                "Makili Pietru as her wings spread and her horns grew and then took shape into a demon.  His wings transformed " +
                "and became like the petals of a purple rose and when he comes out from under the ground, he releases his virus and " +
                "infects everyone. The hunters were warned of this threat and the village chief sent out the hunters who had faced the last Zenith.  " +
                "The fury of the new Zenith made him sprout horns and I began to shine brighter.  His fury and his purple mist suffocated everyone " +
                "and above all killed the whole village.  In the end, you decide the fate and you will be the one to slay the dragon! " +
                "You will be the one to kill him!  And you will face the legend of the Frontier!! "
            ),
            
            new("Beast of Berserk", "images/13th.webp", "images/BerserkRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5411", 
                "Beast of Darknes of Berserk come in new world replaced by aphotom, " +
                "ebony odogoron and shamons for ride it"
            ),
            
            new("True Symbol of Fatalis Velkhana", "images/14th.webp", "images/VelkhanaRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5525", 
                "The true simbol of Fatalis is a Velkana corupted from Nemesis Heroic the first Fatalis. " +
                "Now Velkana launch fire and explosion and for beat you need to complete the quest " +
                "of 2 Velkana of fire."
            ),
            
            new("Thunderlord Zinogre", "images/15th.webp", "images/ThunderlordRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5622", 
                "Thunderlord Zinogre come in this new world for destroid it"
            ),
            
            new("A52-Galaxy Nergigante", "images/16th.webp", "images/GalaxyNeg.webp", "https://www.nexusmods.com/monsterhunterworld/mods/6327", 
                "The A26-Galaxy Nergigante is a monster that comes from another galaxy climbing from its" +
                " world because it was being haunted by space hunters. Arriving in this new world the Nergigante " +
                "was very hungry for revenge for the hunters, his fury that pervaded him had become toxic until " +
                "it came out of his thorns, his golden claws cause a great paralysis and at times he can make the " +
                "hunters bleed and adore them with the its technological wings.  The reason for the enormous hunger of this monster " +
                "is explained by the huge black hole that in the stomach to devour anyone and his eyes are like those of a Saturn storm. " +
                "Upon the arrival of this mystical monster, the commander was unable to create a new hunt for his fear of this monster. " +
                "It was a danger and every day it bellowed to grow more until it looked like it could devour the world. " +
                "You great hunter defeat this space danger and chase it away!"
            ),
            
            new("Thunder Emperor Kirin", "images/17th.webp", "images/17th.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5656", 
                "It's body structure and overall look is similar to the one of a normal Kirin. " +
                "Thunder Lord Kirin is bigger than its counterpart. Its body is colored with gradients of gold, " +
                "its horn has a copper/red color and its mane has a pale grey and yellow gradient. " +
                "Its surrounding aura is also golden."
            ),
            
            new("Zenith Shagaru Magala", "images/18th.webp", "images/ShagaruRender.webp", "https://www.nexusmods.com/monsterhunterrise/mods/2013", 
                "Is hier a Zenith and is hier for a anormaly thanks a monster called R... " +
                "who created great demonic magical power which released the supernatural forces. Go to investigate and " +
                "defeat this Zenith for the good of the world"
            ),
            
            new("Viking Khusala Doara", "images/19th.webp", "images/KhusalaIceRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/6265", 
                "The Vikings long ago had found a Kushala doara and taken by the desire they fought it " +
                "then cutting its wings as humiliation and using them for equipment.  The kusha after " +
                "getting great pain returns to his ice cave where his wings began to regrow and become " +
                "ice with no biological aspect."
            ),
            
            new("Ebony Khusala Doara", "images/20th.webp", "images/EbonyDoaraRender.webp", "https://www.nexusmods.com/monsterhunterworld/mods/5295", 
                "A kushala doara that feeds on ebony crystals and its metal turns black. " +
                "By eating this kind of crystal his body is filled with longer and stronger " +
                "crystals that the purple sharpness causes to bounce off his body. The variant of the " +
                "monster existing only hardened arch in the mission found at the ancient forest"
            )
        };
    }
}