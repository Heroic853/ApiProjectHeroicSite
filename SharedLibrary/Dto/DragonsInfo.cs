using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class DragonsInfo
    {
        public static readonly string[] Class = new[]
        {
        "Elder Dragon", "Lynians", "Bird Wyvern", "Pelagus", "Fanged Beasts", "Leviathans", "Unknown", "Relict", "Brute Wyvern", "Snake Wyvern" , "Herbivore" , "Fish" , "Wingdrake" ,
        "Neopteron" ,"Temnoceran" , "Flying Wyvern" , "Piscine Wyvern" ,  "Carapaceon" , "Amphibian" , "Primatius" , "Fanged Wyvern"
        };

        public static readonly string[] Elements = new[]
        {
        "Dragon", "Fire", "Blood", "Water", "Thunder", "Frenzy", "Ice", "Earth", "Nature", "Stun", "Bubble", "Blaze", "Light", "Tenshou", "Frozen", "Burning Zero",
        "Sound", "Wind", "Crimson Demon", "Darkness", "Black Flame", "Kanade", "Thunder Pole"
        };

        public static readonly string[] Names = new[]
        {
        "Fatalis", "Rathalos", "Seregios", "Lagiacrus", "Agnaktor", "Safi,jiiva", "Tigrex", "Glavenus", "Gran Jagras", "Astalos", "Zinogre", "Nargacuga",
        "Valstrax", "Mizutsune", "Khezu","Felyne", "Yian Garuga", "Malfestio", "Diablos", "Pukey Pukey", "Basarios", "Monoblos", "Bazelgeuse", "Espinas",
        "Legiana", "Zerureusu", "Barioth", "Mi Ru", "Diorekkusu", "Plesioth", "Lavasioth", "Tigrex", "Glavenus", "Rathian", "Keokoboru", "Nergigante", "Vaal Hazak",
        "Teostra", "Lunastra", "Khusha Doara", "Zorah Magmados" , "Iodrome" , "Lao-Shan Lung" , "Kirin" , "Silver Rathalos" , "Purple Gypceros" , "Crimson Fatalis" ,
        "White Fatalis" , "Scarred Yian Garuga" , "Giadrome" , "Daimyo Hermitaur" , "Yama Tsukami" , "Chameleos" , "Hypnocatrice" , "Ukanlos" , "Gobul" ,  "Nibelsnarf" ,
        "Savage Deviljho" , "Molten Tigrex" , "Dalamadur" , "Shas Dalamadur" , "Risen Teostra" , "Risen Chameleos" , "Primordial Malzeno" , "Ivory Lagiacrus" ,
        "Abyssal Lagiacrus" , "Risen Khusha Doara" , "Risen Crimson Glow Valstrax" , "Crimson Glow Valstrax" , "Draeking Rathalos" , "Draequeen Rathian" , "White Espinas" ,
        "Magnamalo" , "Thunderlord Zinogre" , "Stygian Zinogre" , "Xeno' Jiiva" , "Risen Shagaru Magala" , "Shagaru Magala" , "Chaotic Gore Magala" , "Gore Magala" , "Silverwind Nargacuga" ,
        "Green Nargacuga" , "Lucent Nargacuga" , "Aknasom" , "Azure Rathalos" , "Pink Rathian" , "White Monoblos" , "Red Khezu" , "Rusted Khusa Doara" , "Shen Gaoren" , "Qurupeco" , "Lagombi" ,
        "Goldenbear Ceadeus" , "Oroshi Kirin" , "Raging Brachidios" , "Gogmazios" , "Hellbalde Glavenus" , "Boltreaver Astalos" , "Bloodbath Diablos" , "Fulgur Anjanath", "Rey Dau", 
            "Arkveld", "Zoh Shia", "Seikret", "Balahara", "Ajarakan", "Doshaguma", "Guardian Rathalos", "Guardian Fulgur Anjanath", "Guardian Arkveld", "Chatacabra", "Guardian Doshaguma",
            "Guardian Ebony Odogaron", "Hirabami", "Jin Dahaad", "Lala Barina", "Nu Undra", "Omega Planetes","Quematrice", "Rompopolo", "Uth Duna", "Xu Wu"
        };

        public static readonly string[] Map = new[]
        {
        "Desert", "Fortress", "Volcano", "Arena", "Castle Schrade", "Swamp", "Snowy Mountains", "Town", "Tower", "Tundra", "Polar Field", "Sacred Land",
        "Dunes", "Artic Ridge", "Citadel", "Elder Racess", "Guiding Lands", "Rotten Vale", "Coral Highlands", "Origin Isle", "Coral Palace", "Infernal Springs" ,
        "Forest and Hills" , "Great Arena" , "Jungle" , "Moat Arena" , "Deserted Island" , "Sacred Land" , "Tainted Sea" , "Underwater Ruin" , "Tainted Sea" ,
        "Sacred Pinnacle" , "Lava Canyon" , "Water Arena" , "Primal Forest" , "Jurassic Frontier" , "Dunes" , "Sanctuary" , "Speartip Crag" , "Great Sea" , "Frozen Seaway" ,
        "Ancestral Steppe" , "Sunken Hollow" , "Heaven's Mount" , "Verdant Hills" , "Wyvern's End" , "Forlorn Citadel" , "Hoarfrost Reach" , "Gorge" , "Deep Crater" ,
        "Solitude Island" , "Sky Corridor" , "Historical Site" , "Bamboo Forest" , "Moonlands" , "Glacial Valley" , "Esther Lake", "Scarlet Forest", "Iceshard Cliffs",
        "Windward Plains", "Oilwell Basin", "Ruins of Wyveria"
        };

        public static readonly string[] Moded = new[]
        {
        "Primordial Risen Alatreon", "Maleficent as Dragon", "Zenith Gore Magala True frenzy","Phantom Hallowed Xenojiiva", "A52-Galaxy Nergigante new universe", "Blinking Nargacuga from MHFZ",
        "Viking Kushala doara", "Rathalos From Mh1 Commision", "IMMORTAL ZINOGRE", "DESTROID TIGREX XENO" , "DESTRUCTION WYVERN RATHALOS FROM MHXR", "TRUE UNKNOWN SOLTRICE WAR THE DEMON",
        "Sturdy Kusha Doara", "Smelly STINKYN  Alatreon", "Slime Stiky namielle", "FIERCE WATER NARGACUGA", "Zenith Shagaru Magala True Frenzy", "ATLANTIS FATALIS WATER ELEMENT", "Aberrant deviljho Giant Colosal from AOT",
        "Velkana -0 Celtus Power From Neptun Ice", "Dancan The Werewolf", "Scorching heat Rathian and Flame Rathalos From MHXR", "Thunder Emperor Kirin", "Shara Demon Ice Revive", "Redwolf", "Thunderlord Zinogre",
        "Golden King Nergigante Fire X Ice", "Rocky Uragaan Ice from MHXR EXPLORE NEW DEVARIANT", "Demon ice and fire Safi'Jiiva SUPREME FURRY", "True Simbol of fatalis Velkana", "Zood imortal Berserk",
        "Kulve Demon", "God Alatreon", "Beast of darkness Berserk", "Zinogre Cremisi", "Apex Mizutzune", "Varusaburosu",  "Ebony Kushala Doara", "Configuration Hellblade Glavenus", "Armageddon Amatsu",
        "Raum The Devil King", "Normal Valstrax", "CrimsonDemon Fatalis the Dread", "Sparkling Zerureusu 20th Birthday"
        };

        public static readonly string[] Feedbacks = new[]
        {
        "10" , "9" ,"8" ,"7", "6", "5", "4", "3", "2", "1", "0", "I like him", "Good", "Bad" , "i hate" , "Wonderfull" , "was a bad idea", "I need more" , 
        "Amazing colors and effects" , "Need a video for this monster","Why exist his mod?", "Made a diferent monster like this" ,
        " i want a monster like this in another game"
        };
    }
}
