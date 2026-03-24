using ELKH.Models;

namespace ELKH.Data;

/// <summary>
/// Product seed data partial class containing 416 sticker products across 11 themed categories.
/// This file is part of the database seeding strategy used during application initialization.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS - PRODUCT CATEGORIES
/// ================================================================================
/// Category Name         | Product Count | Lines        | Description
/// ---------------------|---------------|--------------|---------------------------
/// CANADIAN             | 40 products   | 29-69        | Canadian-themed stickers
/// CHRISTMAS            | 43 products   | 71-115       | Christmas & holiday stickers
/// ANIMALS              | 52 products   | 117-170      | Wildlife & pet stickers
/// EASTER               | 34 products   | 172-207      | Easter & spring stickers
/// FOOD & DRINK         | 49 products   | 209-259      | Food-themed stickers
/// HALLOWEEN            | 38 products   | 261-300      | Halloween & spooky stickers
/// LUNAR NEW YEAR       | 24 products   | 302-327      | Lunar New Year stickers
/// NATURE & OUTDOORS    | 43 products   | 329-373      | Nature & landscape stickers
/// NEW YEAR             | 22 products   | 375-398      | New Year celebration stickers
/// THANKSGIVING         | 31 products   | 400-432      | Thanksgiving & autumn stickers
/// MISCELLANEOUS        | 40 products   | 434-475      | General & novelty stickers
/// ================================================================================
///
/// USAGE:
/// Called by DbSeeder.SeedProducts() during application startup to populate the
/// Products table if it's empty. Each product includes:
/// - Name, Description, Price, Discount%, Stock Quantity
/// - Category assignment
/// - Search tags for discovery
///
/// PRICING STRATEGY:
/// - Base price range: $1.99 - $4.99
/// - Discount percentages: 0%, 10%, 15%, 20%, 25%
/// - Out-of-stock items (0 quantity) strategically placed for testing
///
/// STOCK MANAGEMENT:
/// - Most items: 100-320 units in stock
/// - Low stock items: 15-95 units (testing low-stock alerts)
/// - Out of stock: 0 units (testing sold-out behavior)
///
/// SEARCH OPTIMIZATION:
/// Each product tagged with 4 comma-separated keywords for:
/// - Fuzzy search matching
/// - Category filtering
/// - Related product discovery
///
/// HELPER METHOD:
/// P() - Shorthand factory method to create ProductModel instances
///      Parameters: name, description, price, discount%, stock, category, tags
/// </remarks>
public static partial class DbSeeder
{
    private static List<ProductModel> GetProducts(
        CategoryModel canadian,
        CategoryModel christmas,
        CategoryModel animals,
        CategoryModel easter,
        CategoryModel food,
        CategoryModel halloween,
        CategoryModel lunarNY,
        CategoryModel nature,
        CategoryModel newYear,
        CategoryModel thanks,
        CategoryModel misc)
    {
        return new List<ProductModel>
        {
            // ══════════════════════════════════════════════════════════════
            // CANADIAN (40 products)
            // ══════════════════════════════════════════════════════════════
            P("Maple Leaf Pride Sticker", "Classic red maple leaf design celebrating Canadian heritage.", 2.49m, 0, 250, canadian, "canadian,maple,leaf,patriotic", isBestSeller: true),
            P("Eh Canada Sticker", "Friendly 'Eh?' text design with Canadian flag colors.", 1.99m, 10, 180, canadian, "canadian,eh,funny,text", isTrending: true),
            P("Hockey Night Sticker", "Hockey stick and puck in red and white.", 2.99m, 0, 200, canadian, "canadian,hockey,sports,winter", isBestSeller: true),
            P("Mountie Bear Sticker", "Cute bear wearing RCMP uniform.", 3.49m, 0, 0, canadian, "canadian,mountie,bear,cute"),
            P("Poutine Love Sticker", "Delicious poutine illustration with heart.", 2.49m, 15, 150, canadian, "canadian,poutine,food,quebec", isTrending: true),
            P("Beaver Builder Sticker", "Cartoon beaver with hard hat and tools.", 2.99m, 0, 175, canadian, "canadian,beaver,animal,builder"),
            P("Canoe Adventure Sticker", "Red canoe on calm lake with pine trees.", 3.99m, 0, 120, canadian, "canadian,canoe,nature,adventure"),
            P("Toronto Skyline Sticker", "CN Tower and cityscape silhouette.", 3.49m, 0, 140, canadian, "canadian,toronto,city,skyline"),
            P("Inukshuk Stone Sticker", "Traditional stone landmark symbol.", 2.99m, 0, 160, canadian, "canadian,inukshuk,symbol,north"),
            P("Tim Hortons Coffee Sticker", "Coffee cup with maple leaf design.", 2.49m, 20, 15, canadian, "canadian,coffee,tim-hortons,drink"),
            P("Polar Bear North Sticker", "Majestic polar bear on ice.", 3.99m, 0, 110, canadian, "canadian,polar-bear,animal,arctic"),
            P("Vancouver Mountains Sticker", "Mountain range with ocean view.", 3.49m, 0, 130, canadian, "canadian,vancouver,mountains,nature"),
            P("Toonie Coin Sticker", "Canadian $2 coin illustration.", 1.99m, 0, 190, canadian, "canadian,toonie,coin,money"),
            P("Moose Crossing Sticker", "Moose silhouette with warning sign style.", 2.99m, 10, 145, canadian, "canadian,moose,animal,sign"),
            P("Quebec Fleur-de-lis Sticker", "Traditional Quebec symbol in blue.", 2.49m, 0, 155, canadian, "canadian,quebec,fleur-de-lis,symbol"),
            P("Niagara Falls Sticker", "Waterfall landscape illustration.", 3.99m, 0, 100, canadian, "canadian,niagara,falls,nature"),
            P("Lumberjack Plaid Sticker", "Red and black plaid pattern square.", 1.99m, 25, 200, canadian, "canadian,plaid,pattern,lumberjack"),
            P("Canadian Goose Sticker", "Honking Canada goose in flight.", 2.99m, 0, 135, canadian, "canadian,goose,bird,wildlife"),
            P("Montreal Bagel Sticker", "Sesame seed bagel illustration.", 2.49m, 0, 165, canadian, "canadian,montreal,bagel,food"),
            P("Hockey Goalie Sticker", "Goalie mask with maple leaf design.", 3.49m, 15, 125, canadian, "canadian,hockey,goalie,sports"),
            P("Northern Lights Canada Sticker", "Aurora borealis over snowy landscape.", 4.49m, 0, 90, canadian, "canadian,aurora,lights,nature"),
            P("Parliament Hill Sticker", "Ottawa Parliament buildings illustration.", 3.99m, 0, 105, canadian, "canadian,ottawa,parliament,landmark"),
            P("Salmon Migration Sticker", "Pacific salmon jumping upstream.", 3.49m, 0, 115, canadian, "canadian,salmon,fish,nature"),
            P("Maple Syrup Bottle Sticker", "Glass bottle with maple syrup and leaf.", 2.99m, 10, 170, canadian, "canadian,maple-syrup,food,sweet"),
            P("Lacrosse Stick Sticker", "Traditional lacrosse stick and ball.", 2.49m, 0, 150, canadian, "canadian,lacrosse,sports,indigenous"),
            P("Prairie Wheat Sticker", "Golden wheat stalks blowing in wind.", 2.99m, 0, 140, canadian, "canadian,prairie,wheat,agriculture"),
            P("Ice Hockey Rink Sticker", "Top-down view of hockey rink.", 3.49m, 0, 130, canadian, "canadian,hockey,rink,sports"),
            P("Bluenose Schooner Sticker", "Famous Nova Scotia sailing ship.", 3.99m, 20, 95, canadian, "canadian,bluenose,ship,maritime"),
            P("Caribou Sticker", "Majestic caribou with large antlers.", 3.49m, 0, 120, canadian, "canadian,caribou,animal,wildlife"),
            P("Canadian Shield Sticker", "Rocky landscape of the Shield region.", 2.99m, 0, 145, canadian, "canadian,shield,nature,geology"),
            P("Butter Tart Sticker", "Classic Canadian butter tart pastry.", 2.49m, 0, 160, canadian, "canadian,butter-tart,food,dessert"),
            P("Nanaimo Bar Sticker", "Layered chocolate dessert square.", 2.99m, 15, 155, canadian, "canadian,nanaimo,dessert,chocolate"),
            P("Loonie Coin Sticker", "Canadian $1 coin with loon.", 1.99m, 0, 185, canadian, "canadian,loonie,coin,money"),
            P("Igloo Home Sticker", "Traditional snow igloo structure.", 2.49m, 0, 165, canadian, "canadian,igloo,home,arctic"),
            P("Bannock Bread Sticker", "Traditional indigenous fry bread.", 2.99m, 0, 0, canadian, "canadian,bannock,bread,indigenous"),
            P("Coast Redwoods Sticker", "Tall BC coastal redwood trees.", 3.49m, 0, 125, canadian, "canadian,redwood,tree,nature"),
            P("Lobster Trap Sticker", "Maritime lobster trap with buoy.", 3.99m, 10, 100, canadian, "canadian,lobster,maritime,seafood"),
            P("Curling Stone Sticker", "Red curling stone with handle.", 2.99m, 0, 140, canadian, "canadian,curling,sports,winter"),
            P("Ketchup Chips Sticker", "Iconic Canadian chip flavor bag.", 2.49m, 0, 175, canadian, "canadian,chips,ketchup,snack"),
            P("Rocky Mountains Sticker", "Alberta Rocky Mountain peaks.", 4.49m, 0, 85, canadian, "canadian,rockies,mountains,alberta"),

            // ══════════════════════════════════════════════════════════════
            // CHRISTMAS (43 products)
            // ══════════════════════════════════════════════════════════════
            P("Santa Claus Face Sticker", "Jolly Santa with rosy cheeks and white beard.", 2.99m, 0, 300, christmas, "christmas,santa,holiday,festive", isBestSeller: true, isTrending: true),
            P("Christmas Tree Sticker", "Decorated evergreen tree with star topper.", 2.49m, 15, 250, christmas, "christmas,tree,ornament,festive", isBestSeller: true),
            P("Snowflake Crystal Sticker", "Intricate six-pointed snowflake design.", 1.99m, 0, 280, christmas, "christmas,snowflake,winter,snow", isTrending: true),
            P("Candy Cane Sticker", "Classic red and white striped candy cane.", 1.99m, 20, 320, christmas, "christmas,candy-cane,sweet,peppermint"),
            P("Gingerbread Man Sticker", "Smiling gingerbread cookie with icing.", 2.49m, 0, 260, christmas, "christmas,gingerbread,cookie,baking"),
            P("Reindeer Rudolph Sticker", "Red-nosed reindeer with antlers.", 2.99m, 0, 240, christmas, "christmas,reindeer,rudolph,animal"),
            P("Christmas Wreath Sticker", "Green wreath with red bow and berries.", 3.49m, 0, 200, christmas, "christmas,wreath,decoration,festive"),
            P("Snowman Frosty Sticker", "Snowman with carrot nose and scarf.", 2.99m, 10, 220, christmas, "christmas,snowman,winter,snow"),
            P("Holly Berries Sticker", "Red berries with green holly leaves.", 2.49m, 0, 270, christmas, "christmas,holly,berries,decoration"),
            P("Christmas Bell Sticker", "Gold bell with red ribbon.", 2.99m, 0, 230, christmas, "christmas,bell,gold,ribbon"),
            P("Elf Helper Sticker", "Cute elf with pointed hat and shoes.", 2.99m, 0, 210, christmas, "christmas,elf,helper,santa"),
            P("Gift Box Sticker", "Wrapped present with bow on top.", 2.49m, 15, 250, christmas, "christmas,gift,present,wrapping"),
            P("Christmas Lights Sticker", "String of colorful bulb lights.", 2.99m, 0, 235, christmas, "christmas,lights,decoration,colorful"),
            P("Poinsettia Flower Sticker", "Red poinsettia Christmas flower.", 3.49m, 0, 180, christmas, "christmas,poinsettia,flower,red"),
            P("Hot Cocoa Sticker", "Mug of hot chocolate with marshmallows.", 2.49m, 0, 265, christmas, "christmas,cocoa,drink,warm"),
            P("Nutcracker Soldier Sticker", "Traditional nutcracker in red uniform.", 3.99m, 0, 150, christmas, "christmas,nutcracker,soldier,toy"),
            P("Christmas Stocking Sticker", "Red stocking hanging with care.", 2.99m, 10, 215, christmas, "christmas,stocking,tradition,gift"),
            P("North Pole Sign Sticker", "Wooden sign pointing to North Pole.", 2.49m, 0, 255, christmas, "christmas,north-pole,sign,santa"),
            P("Christmas Star Sticker", "Golden star with sparkle effects.", 2.99m, 0, 245, christmas, "christmas,star,gold,decoration"),
            P("Sleigh Bells Sticker", "Silver bells on red ribbon.", 2.49m, 20, 15, christmas, "christmas,bells,sleigh,jingle"),
            P("Mrs. Claus Sticker", "Kind Mrs. Claus with apron and glasses.", 2.99m, 0, 195, christmas, "christmas,mrs-claus,santa,baking"),
            P("Mistletoe Sticker", "Green mistletoe with white berries.", 2.49m, 0, 260, christmas, "christmas,mistletoe,tradition,kiss"),
            P("Christmas Ornament Sticker", "Shiny red ball ornament.", 1.99m, 15, 290, christmas, "christmas,ornament,ball,decoration"),
            P("Advent Calendar Sticker", "24-door calendar counting to Christmas.", 3.49m, 0, 170, christmas, "christmas,advent,calendar,countdown"),
            P("Yule Log Sticker", "Chocolate yule log cake with holly.", 3.99m, 0, 140, christmas, "christmas,yule-log,cake,dessert"),
            P("Christmas Candle Sticker", "Red candle with flame and holly.", 2.99m, 0, 220, christmas, "christmas,candle,flame,decoration"),
            P("Penguin Santa Hat Sticker", "Cute penguin wearing Santa hat.", 2.49m, 0, 270, christmas, "christmas,penguin,santa-hat,cute"),
            P("Christmas Cardinal Sticker", "Red cardinal bird on snowy branch.", 3.49m, 10, 185, christmas, "christmas,cardinal,bird,winter"),
            P("Jingle Bells Sticker", "Three silver bells tied with ribbon.", 2.99m, 0, 230, christmas, "christmas,jingle,bells,music"),
            P("Fireplace Stocking Sticker", "Mantle with stockings and fire.", 3.99m, 0, 145, christmas, "christmas,fireplace,stocking,cozy"),
            P("Christmas Cookies Sticker", "Plate of decorated sugar cookies.", 2.49m, 0, 275, christmas, "christmas,cookies,baking,sweet"),
            P("Snowglobe Sticker", "Snow globe with Christmas scene inside.", 3.49m, 15, 165, christmas, "christmas,snowglobe,decoration,snow"),
            P("Christmas Puppy Sticker", "Puppy wearing reindeer antlers.", 2.99m, 0, 235, christmas, "christmas,puppy,dog,cute"),
            P("Festive Train Sticker", "Toy train carrying presents.", 3.99m, 0, 135, christmas, "christmas,train,toy,presents"),
            P("Angel Ornament Sticker", "White angel with golden halo.", 3.49m, 0, 175, christmas, "christmas,angel,ornament,heavenly"),
            P("Christmas Village Sticker", "Snowy village scene with houses.", 4.49m, 10, 120, christmas, "christmas,village,snow,houses"),
            P("Peppermint Swirl Sticker", "Red and white peppermint candy.", 1.99m, 0, 310, christmas, "christmas,peppermint,candy,sweet"),
            P("Christmas Bear Sticker", "Teddy bear with Santa hat and scarf.", 2.99m, 0, 225, christmas, "christmas,bear,teddy,cute"),
            P("Holiday Garland Sticker", "Pine garland with red bows.", 3.49m, 0, 180, christmas, "christmas,garland,decoration,pine"),
            P("Christmas Mailbox Sticker", "Red mailbox for letters to Santa.", 2.99m, 15, 210, christmas, "christmas,mailbox,santa,letters"),
            P("Winter Cabin Sticker", "Cozy log cabin in snow.", 3.99m, 0, 0, christmas, "christmas,cabin,winter,cozy"),
            P("Christmas Kitten Sticker", "Kitten playing with ornament.", 2.49m, 0, 265, christmas, "christmas,kitten,cat,cute"),
            P("Peace on Earth Sticker", "Dove with olive branch and text.", 2.99m, 0, 240, christmas, "christmas,peace,dove,message"),

            // ══════════════════════════════════════════════════════════════
            // CUTE ANIMALS (50 products)
            // ══════════════════════════════════════════════════════════════
            P("Kawaii Panda Sticker", "Adorable panda munching bamboo.", 2.49m, 0, 280, animals, "cute,panda,kawaii,animal"),
            P("Baby Elephant Sticker", "Little elephant with big ears.", 2.99m, 15, 250, animals, "cute,elephant,baby,animal"),
            P("Chibi Cat Sticker", "Super deformed cat with big eyes.", 1.99m, 0, 320, animals, "cute,cat,chibi,kawaii"),
            P("Sleepy Sloth Sticker", "Lazy sloth hanging from branch.", 2.49m, 0, 290, animals, "cute,sloth,lazy,animal"),
            P("Happy Hedgehog Sticker", "Smiling hedgehog with tiny feet.", 2.99m, 0, 270, animals, "cute,hedgehog,happy,animal"),
            P("Corgi Butt Sticker", "Fluffy corgi from behind.", 2.49m, 20, 300, animals, "cute,corgi,dog,fluffy"),
            P("Bunny Rabbit Sticker", "Floppy-eared bunny with carrot.", 2.99m, 0, 260, animals, "cute,bunny,rabbit,carrot"),
            P("Otter Love Sticker", "Two otters holding hands.", 3.49m, 0, 220, animals, "cute,otter,love,couple"),
            P("Hamster Cheeks Sticker", "Hamster with stuffed cheeks.", 2.49m, 10, 285, animals, "cute,hamster,cheeks,animal"),
            P("Fox Kit Sticker", "Baby fox with fluffy tail.", 2.99m, 0, 265, animals, "cute,fox,baby,animal"),
            P("Penguin Waddle Sticker", "Cute penguin waddling.", 2.49m, 0, 295, animals, "cute,penguin,waddle,bird"),
            P("Guinea Pig Sticker", "Fluffy guinea pig eating lettuce.", 2.99m, 0, 255, animals, "cute,guinea-pig,fluffy,pet"),
            P("Red Panda Sticker", "Adorable red panda on tree.", 3.49m, 15, 230, animals, "cute,red-panda,tree,animal"),
            P("Chick Baby Sticker", "Yellow baby chick hatching.", 1.99m, 0, 310, animals, "cute,chick,baby,bird"),
            P("Axolotl Sticker", "Pink smiling axolotl salamander.", 2.99m, 0, 245, animals, "cute,axolotl,pink,aquatic"),
            P("Seal Pup Sticker", "Baby seal with big eyes.", 2.49m, 0, 275, animals, "cute,seal,pup,ocean"),
            P("Shiba Inu Sticker", "Smiling Shiba Inu dog.", 2.99m, 10, 240, animals, "cute,shiba-inu,dog,japanese"),
            P("Koala Hug Sticker", "Koala hugging eucalyptus branch.", 3.49m, 0, 215, animals, "cute,koala,hug,australia"),
            P("Duckling Sticker", "Yellow duckling with orange feet.", 2.49m, 0, 285, animals, "cute,duckling,yellow,bird"),
            P("Capybara Sticker", "Chill capybara sitting in water.", 2.99m, 0, 250, animals, "cute,capybara,chill,animal"),
            P("Chipmunk Sticker", "Chipmunk with acorn.", 2.49m, 20, 10, animals, "cute,chipmunk,acorn,animal"),
            P("Llama Sticker", "Fluffy llama with flowers.", 3.49m, 0, 225, animals, "cute,llama,fluffy,flowers"),
            P("Raccoon Sticker", "Mischievous raccoon with mask.", 2.99m, 0, 255, animals, "cute,raccoon,mischievous,animal"),
            P("Frog Prince Sticker", "Cute frog with tiny crown.", 2.49m, 15, 270, animals, "cute,frog,prince,crown"),
            P("Narwhal Sticker", "Smiling narwhal with horn.", 2.99m, 0, 240, animals, "cute,narwhal,unicorn,ocean"),
            P("Bumblebee Sticker", "Fuzzy bumblebee on flower.", 2.49m, 0, 280, animals, "cute,bee,bumblebee,insect"),
            P("Baby Deer Sticker", "Fawn with white spots.", 3.49m, 0, 210, animals, "cute,deer,fawn,baby"),
            P("Chinchilla Sticker", "Fluffy chinchilla taking dust bath.", 2.99m, 10, 235, animals, "cute,chinchilla,fluffy,pet"),
            P("Owl Hoot Sticker", "Round owl with big eyes.", 2.49m, 0, 275, animals, "cute,owl,bird,eyes"),
            P("Turtle Tot Sticker", "Baby sea turtle swimming.", 2.99m, 0, 245, animals, "cute,turtle,baby,ocean"),
            P("Pomeranian Sticker", "Fluffy Pomeranian dog.", 3.49m, 15, 220, animals, "cute,pomeranian,dog,fluffy"),
            P("Lamb Sticker", "Woolly lamb with pink ears.", 2.49m, 0, 270, animals, "cute,lamb,sheep,woolly"),
            P("Platypus Sticker", "Quirky platypus swimming.", 2.99m, 0, 250, animals, "cute,platypus,quirky,australia"),
            P("Squirrel Acorn Sticker", "Squirrel holding acorn.", 2.49m, 0, 265, animals, "cute,squirrel,acorn,animal"),
            P("Piglet Sticker", "Pink baby pig with curly tail.", 2.99m, 10, 240, animals, "cute,pig,piglet,farm"),
            P("Butterfly Sticker", "Colorful butterfly with big wings.", 2.49m, 0, 285, animals, "cute,butterfly,colorful,insect"),
            P("Jellyfish Sticker", "Pastel jellyfish floating.", 2.99m, 0, 255, animals, "cute,jellyfish,pastel,ocean"),
            P("Quokka Smile Sticker", "Smiling quokka selfie pose.", 3.49m, 20, 200, animals, "cute,quokka,smile,happy"),
            P("Mouse Cheese Sticker", "Tiny mouse nibbling cheese.", 2.49m, 0, 280, animals, "cute,mouse,cheese,tiny"),
            P("Seahorse Sticker", "Delicate seahorse in coral.", 2.99m, 0, 245, animals, "cute,seahorse,coral,ocean"),
            P("Ferret Sticker", "Playful ferret peeking out.", 2.49m, 15, 260, animals, "cute,ferret,playful,pet"),
            P("Alpaca Sticker", "Fluffy alpaca with smile.", 3.49m, 0, 215, animals, "cute,alpaca,fluffy,smile"),
            P("Snail Trail Sticker", "Happy snail with spiral shell.", 1.99m, 0, 295, animals, "cute,snail,shell,slow"),
            P("Starfish Sticker", "Smiling pink starfish.", 2.49m, 0, 275, animals, "cute,starfish,pink,ocean"),
            P("Meerkat Sticker", "Alert meerkat standing guard.", 2.99m, 10, 235, animals, "cute,meerkat,alert,animal"),
            P("Ladybug Sticker", "Red ladybug with black spots.", 1.99m, 0, 305, animals, "cute,ladybug,red,insect"),
            P("Walrus Sticker", "Chubby walrus with tusks.", 2.99m, 0, 0, animals, "cute,walrus,tusks,ocean"),
            P("Bat Hanging Sticker", "Cute bat hanging upside down.", 2.49m, 0, 270, animals, "cute,bat,hanging,night"),
            P("Puffin Sticker", "Colorful puffin bird with fish.", 3.49m, 0, 205, animals, "cute,puffin,bird,colorful"),
            P("Sugar Glider Sticker", "Tiny sugar glider gliding.", 2.99m, 15, 230, animals, "cute,sugar-glider,gliding,small"),

            // ══════════════════════════════════════════════════════════════
            // EASTER (35 products)
            // ══════════════════════════════════════════════════════════════
            P("Easter Bunny Sticker", "White bunny with basket of eggs.", 2.99m, 0, 200, easter, "easter,bunny,rabbit,eggs"),
            P("Decorated Egg Sticker", "Colorfully painted Easter egg.", 1.99m, 15, 250, easter, "easter,egg,painted,decorated"),
            P("Chick Hatching Sticker", "Baby chick breaking out of egg.", 2.49m, 0, 220, easter, "easter,chick,hatching,baby"),
            P("Easter Basket Sticker", "Woven basket filled with eggs.", 3.49m, 0, 180, easter, "easter,basket,eggs,woven"),
            P("Spring Tulips Sticker", "Colorful tulips in bloom.", 2.99m, 0, 210, easter, "easter,tulips,spring,flowers"),
            P("Bunny Ears Sticker", "Pink bunny ears headband.", 2.49m, 10, 230, easter, "easter,bunny-ears,pink,costume"),
            P("Carrot Patch Sticker", "Orange carrots growing in garden.", 2.99m, 0, 190, easter, "easter,carrot,garden,vegetable"),
            P("Easter Egg Hunt Sticker", "Hidden eggs in grass.", 2.49m, 0, 225, easter, "easter,egg-hunt,grass,hidden"),
            P("Lamb Spring Sticker", "Cute lamb in spring meadow.", 2.99m, 0, 200, easter, "easter,lamb,spring,meadow"),
            P("Daffodil Sticker", "Yellow daffodil flower.", 2.49m, 20, 15, easter, "easter,daffodil,yellow,spring"),
            P("Easter Peeps Sticker", "Marshmallow peep chicks.", 1.99m, 0, 260, easter, "easter,peeps,marshmallow,candy"),
            P("Bunny Footprints Sticker", "Trail of bunny paw prints.", 2.49m, 0, 235, easter, "easter,footprints,bunny,trail"),
            P("Cross Religious Sticker", "Wooden cross with flowers.", 2.99m, 0, 170, easter, "easter,cross,religious,faith"),
            P("Easter Lily Sticker", "White Easter lily flower.", 3.49m, 15, 165, easter, "easter,lily,white,flower"),
            P("Jelly Beans Sticker", "Pile of colorful jelly beans.", 1.99m, 0, 255, easter, "easter,jelly-beans,candy,colorful"),
            P("Spring Butterfly Sticker", "Butterfly on spring flower.", 2.49m, 0, 230, easter, "easter,butterfly,spring,flower"),
            P("Bunny Silhouette Sticker", "Black bunny silhouette.", 2.99m, 0, 205, easter, "easter,bunny,silhouette,shadow"),
            P("Egg Wreath Sticker", "Wreath made of decorated eggs.", 3.49m, 10, 175, easter, "easter,wreath,eggs,decoration"),
            P("Baby Ducks Sticker", "Three yellow ducklings walking.", 2.49m, 0, 220, easter, "easter,ducks,ducklings,spring"),
            P("Easter Bonnet Sticker", "Fancy hat with flowers and ribbon.", 3.99m, 0, 150, easter, "easter,bonnet,hat,fancy"),
            P("Chocolate Bunny Sticker", "Chocolate rabbit wrapped in foil.", 2.99m, 0, 195, easter, "easter,chocolate,bunny,candy"),
            P("Spring Rain Sticker", "April showers with rainbow.", 2.49m, 15, 215, easter, "easter,rain,spring,rainbow"),
            P("Easter Grass Sticker", "Green plastic Easter grass.", 1.99m, 0, 245, easter, "easter,grass,green,basket"),
            P("Bunny Tail Sticker", "Fluffy white cotton tail.", 2.49m, 0, 230, easter, "easter,tail,bunny,fluffy"),
            P("Resurrection Sticker", "Empty tomb with stone rolled away.", 2.99m, 0, 160, easter, "easter,resurrection,religious,tomb"),
            P("Spring Bee Sticker", "Bumblebee pollinating flowers.", 2.49m, 10, 225, easter, "easter,bee,spring,pollinate"),
            P("Painted Egg Pattern Sticker", "Traditional Ukrainian egg design.", 3.49m, 0, 170, easter, "easter,painted,pattern,traditional"),
            P("Easter Cake Sticker", "Decorated Easter celebration cake.", 3.99m, 0, 140, easter, "easter,cake,dessert,decorated"),
            P("Spring Garden Sticker", "Blooming spring flower garden.", 3.49m, 0, 0, easter, "easter,garden,spring,blooming"),
            P("Bunny Family Sticker", "Mother bunny with baby bunnies.", 2.99m, 15, 190, easter, "easter,bunny,family,babies"),
            P("Easter Banner Sticker", "Happy Easter text banner.", 2.49m, 0, 220, easter, "easter,banner,text,happy"),
            P("Egg Carton Sticker", "Carton with colorful Easter eggs.", 2.99m, 0, 200, easter, "easter,carton,eggs,colorful"),
            P("Spring Blossom Sticker", "Cherry blossoms in spring.", 3.49m, 10, 175, easter, "easter,blossom,cherry,spring"),
            P("Easter Prayer Sticker", "Praying hands with cross.", 2.99m, 0, 165, easter, "easter,prayer,hands,religious"),
            P("Bunny Love Sticker", "Two bunnies touching noses.", 2.49m, 0, 210, easter, "easter,bunny,love,couple"),

            // ══════════════════════════════════════════════════════════════
            // FOOD (53 products)
            // ══════════════════════════════════════════════════════════════
            P("Pizza Slice Sticker", "Cheesy pepperoni pizza slice.", 2.49m, 0, 280, food, "food,pizza,cheese,italian"),
            P("Sushi Roll Sticker", "California roll with chopsticks.", 2.99m, 15, 250, food, "food,sushi,japanese,roll"),
            P("Avocado Half Sticker", "Cute avocado with pit heart.", 2.49m, 0, 270, food, "food,avocado,healthy,cute"),
            P("Donut Sprinkles Sticker", "Pink frosted donut with sprinkles.", 1.99m, 0, 300, food, "food,donut,sprinkles,sweet"),
            P("Taco Tuesday Sticker", "Delicious taco with all toppings.", 2.49m, 10, 265, food, "food,taco,mexican,tuesday"),
            P("Ice Cream Cone Sticker", "Waffle cone with swirled ice cream.", 2.99m, 0, 255, food, "food,ice-cream,dessert,cone"),
            P("Coffee Cup Sticker", "Steaming cup of morning coffee.", 2.49m, 0, 275, food, "food,coffee,drink,morning"),
            P("Burger Stack Sticker", "Juicy burger with cheese and veggies.", 2.99m, 0, 245, food, "food,burger,cheese,delicious"),
            P("Ramen Bowl Sticker", "Steaming bowl of ramen noodles.", 3.49m, 20, 220, food, "food,ramen,noodles,japanese"),
            P("Watermelon Slice Sticker", "Juicy red watermelon wedge.", 2.49m, 0, 285, food, "food,watermelon,fruit,summer"),
            P("Cupcake Sticker", "Chocolate cupcake with frosting.", 2.99m, 0, 240, food, "food,cupcake,dessert,chocolate"),
            P("Boba Tea Sticker", "Milk tea with tapioca pearls.", 2.49m, 15, 270, food, "food,boba,tea,tapioca"),
            P("French Fries Sticker", "Golden crispy french fries.", 1.99m, 0, 295, food, "food,fries,potato,crispy"),
            P("Spaghetti Sticker", "Plate of spaghetti with meatballs.", 3.49m, 0, 215, food, "food,spaghetti,pasta,italian"),
            P("Strawberry Sticker", "Sweet red strawberry.", 2.49m, 0, 280, food, "food,strawberry,fruit,red"),
            P("Pineapple Sticker", "Tropical pineapple fruit.", 2.99m, 10, 235, food, "food,pineapple,tropical,fruit"),
            P("Popcorn Sticker", "Buttery movie theater popcorn.", 2.49m, 0, 265, food, "food,popcorn,snack,butter"),
            P("Macaron Sticker", "Colorful French macaron cookies.", 2.99m, 0, 250, food, "food,macaron,french,cookie"),
            P("Bacon Strips Sticker", "Crispy bacon strips.", 2.49m, 15, 255, food, "food,bacon,breakfast,meat"),
            P("Lemon Sticker", "Bright yellow lemon fruit.", 2.49m, 0, 275, food, "food,lemon,citrus,yellow"),
            P("Pancake Stack Sticker", "Stack of pancakes with butter and syrup.", 3.49m, 0, 210, food, "food,pancakes,breakfast,syrup"),
            P("Peanut Butter Jar Sticker", "Jar of creamy peanut butter.", 2.99m, 10, 240, food, "food,peanut-butter,spread,jar"),
            P("Cherry Pair Sticker", "Two red cherries on stem.", 2.49m, 0, 270, food, "food,cherry,fruit,red"),
            P("Grilled Cheese Sticker", "Melty grilled cheese sandwich.", 2.99m, 0, 245, food, "food,sandwich,cheese,grilled"),
            P("Banana Sticker", "Yellow banana fruit.", 1.99m, 20, 15, food, "food,banana,fruit,yellow"),
            P("Croissant Sticker", "Buttery French croissant.", 2.99m, 0, 230, food, "food,croissant,french,pastry"),
            P("Milk Carton Sticker", "Classic milk carton box.", 2.49m, 0, 260, food, "food,milk,dairy,drink"),
            P("Hot Dog Sticker", "Hot dog with mustard and ketchup.", 2.49m, 15, 255, food, "food,hot-dog,sausage,condiments"),
            P("Orange Slice Sticker", "Juicy orange citrus slice.", 2.49m, 0, 270, food, "food,orange,citrus,fruit"),
            P("Pretzel Sticker", "Twisted salted pretzel.", 2.99m, 0, 240, food, "food,pretzel,salty,snack"),
            P("Egg Sunny Side Sticker", "Fried egg sunny side up.", 2.49m, 10, 250, food, "food,egg,fried,breakfast"),
            P("Apple Red Sticker", "Shiny red apple.", 2.49m, 0, 265, food, "food,apple,red,fruit"),
            P("Soda Can Sticker", "Fizzy soda pop can.", 2.99m, 0, 235, food, "food,soda,drink,fizzy"),
            P("Burrito Sticker", "Wrapped bean and cheese burrito.", 2.99m, 15, 230, food, "food,burrito,mexican,wrapped"),
            P("Cookie Chocolate Chip Sticker", "Classic chocolate chip cookie.", 1.99m, 0, 290, food, "food,cookie,chocolate-chip,dessert"),
            P("Carrot Sticker", "Fresh orange carrot.", 2.49m, 0, 260, food, "food,carrot,vegetable,orange"),
            P("Pickle Jar Sticker", "Jar of dill pickles.", 2.99m, 10, 225, food, "food,pickle,jar,dill"),
            P("Peach Sticker", "Fuzzy ripe peach fruit.", 2.49m, 0, 255, food, "food,peach,fruit,fuzzy"),
            P("Muffin Blueberry Sticker", "Blueberry muffin with crumb top.", 2.99m, 0, 240, food, "food,muffin,blueberry,breakfast"),
            P("Grapes Bunch Sticker", "Purple grape cluster.", 2.49m, 20, 10, food, "food,grapes,purple,fruit"),
            P("Broccoli Sticker", "Green broccoli florets.", 2.49m, 0, 250, food, "food,broccoli,vegetable,green"),
            P("Honey Pot Sticker", "Pot of golden honey with dipper.", 2.99m, 0, 235, food, "food,honey,sweet,pot"),
            P("Waffles Sticker", "Belgian waffles with syrup.", 3.49m, 15, 205, food, "food,waffles,breakfast,syrup"),
            P("Tomato Sticker", "Ripe red tomato.", 2.49m, 0, 255, food, "food,tomato,vegetable,red"),
            P("Bread Loaf Sticker", "Fresh baked bread loaf.", 2.99m, 0, 230, food, "food,bread,baked,loaf"),
            P("Kiwi Slice Sticker", "Green kiwi fruit slice.", 2.49m, 10, 245, food, "food,kiwi,fruit,green"),
            P("Cheese Wedge Sticker", "Yellow cheese wedge with holes.", 2.99m, 0, 225, food, "food,cheese,dairy,yellow"),
            P("Cereal Bowl Sticker", "Bowl of cereal with milk.", 2.49m, 0, 250, food, "food,cereal,breakfast,milk"),
            P("Pear Sticker", "Green pear fruit.", 2.49m, 15, 240, food, "food,pear,fruit,green"),
            P("Yogurt Cup Sticker", "Cup of strawberry yogurt.", 2.99m, 0, 220, food, "food,yogurt,dairy,strawberry"),
            P("Corn Cob Sticker", "Yellow corn on the cob.", 2.49m, 0, 245, food, "food,corn,vegetable,yellow"),
            P("Cake Slice Sticker", "Slice of layered birthday cake.", 3.49m, 10, 0, food, "food,cake,dessert,birthday"),
            P("Mushroom Sticker", "Brown mushroom fungi.", 2.49m, 0, 235, food, "food,mushroom,vegetable,fungi"),

            // ══════════════════════════════════════════════════════════════
            // HALLOWEEN (68 products)
            // ══════════════════════════════════════════════════════════════
            P("Jack O'Lantern Sticker", "Carved pumpkin with scary face.", 2.99m, 0, 350, halloween, "halloween,pumpkin,jack-o-lantern,carved"),
            P("Ghost Boo Sticker", "Cute white ghost saying boo.", 2.49m, 15, 320, halloween, "halloween,ghost,boo,spooky"),
            P("Black Cat Sticker", "Black cat with arched back.", 2.49m, 0, 330, halloween, "halloween,black-cat,cat,spooky"),
            P("Witch Hat Sticker", "Pointy black witch hat.", 2.99m, 0, 310, halloween, "halloween,witch,hat,magic"),
            P("Candy Corn Sticker", "Classic tri-color candy corn.", 1.99m, 10, 340, halloween, "halloween,candy-corn,candy,sweet"),
            P("Spider Web Sticker", "Intricate spider web design.", 2.49m, 0, 325, halloween, "halloween,spider-web,web,creepy"),
            P("Bat Wings Sticker", "Black bat with spread wings.", 2.99m, 0, 300, halloween, "halloween,bat,wings,night"),
            P("Skull Sticker", "White skull with eye sockets.", 2.49m, 20, 15, halloween, "halloween,skull,skeleton,death"),
            P("Frankenstein Sticker", "Green Frankenstein monster face.", 2.99m, 0, 290, halloween, "halloween,frankenstein,monster,green"),
            P("Vampire Fangs Sticker", "Red lips with white fangs.", 2.49m, 0, 315, halloween, "halloween,vampire,fangs,blood"),
            P("Cauldron Brew Sticker", "Bubbling witch's cauldron.", 3.49m, 15, 270, halloween, "halloween,cauldron,witch,potion"),
            P("Mummy Wrapped Sticker", "Mummy wrapped in bandages.", 2.99m, 0, 285, halloween, "halloween,mummy,wrapped,egypt"),
            P("Trick or Treat Sticker", "Trick or treat bag text.", 2.49m, 0, 320, halloween, "halloween,trick-or-treat,bag,candy"),
            P("Full Moon Sticker", "Large orange full moon.", 2.99m, 10, 295, halloween, "halloween,moon,full-moon,night"),
            P("Zombie Hand Sticker", "Green zombie hand reaching up.", 2.49m, 0, 305, halloween, "halloween,zombie,hand,undead"),
            P("Witch Broom Sticker", "Flying broomstick.", 2.99m, 0, 280, halloween, "halloween,broom,witch,flying"),
            P("Poison Bottle Sticker", "Green poison bottle with skull label.", 3.49m, 15, 260, halloween, "halloween,poison,bottle,danger"),
            P("Haunted House Sticker", "Spooky old haunted mansion.", 3.99m, 0, 240, halloween, "halloween,haunted-house,mansion,spooky"),
            P("Werewolf Sticker", "Howling werewolf at moon.", 2.99m, 0, 275, halloween, "halloween,werewolf,howl,moon"),
            P("Eyeball Sticker", "Bloodshot eyeball looking.", 2.49m, 10, 310, halloween, "halloween,eyeball,eye,creepy"),
            P("Skeleton Sticker", "Full dancing skeleton.", 2.99m, 0, 285, halloween, "halloween,skeleton,bones,dancing"),
            P("Candy Bucket Sticker", "Orange pumpkin candy bucket.", 2.49m, 0, 315, halloween, "halloween,bucket,candy,pumpkin"),
            P("Vampire Bat Sticker", "Vampire bat with fangs.", 2.99m, 20, 10, halloween, "halloween,vampire,bat,fangs"),
            P("Coffin Sticker", "Black wooden coffin.", 3.49m, 0, 255, halloween, "halloween,coffin,death,burial"),
            P("Creepy Tree Sticker", "Dead tree with twisted branches.", 2.99m, 0, 280, halloween, "halloween,tree,creepy,dead"),
            P("Goblin Sticker", "Green goblin creature.", 2.49m, 15, 290, halloween, "halloween,goblin,creature,green"),
            P("Scarecrow Sticker", "Straw scarecrow in field.", 2.99m, 0, 270, halloween, "halloween,scarecrow,straw,field"),
            P("Gravestone RIP Sticker", "Tombstone with RIP text.", 2.49m, 0, 300, halloween, "halloween,gravestone,tombstone,rip"),
            P("Witch Wand Sticker", "Magic wand with star.", 2.99m, 10, 275, halloween, "halloween,wand,magic,star"),
            P("Dracula Sticker", "Count Dracula vampire lord.", 3.49m, 0, 250, halloween, "halloween,dracula,vampire,count"),
            P("Pumpkin Patch Sticker", "Field of pumpkins.", 3.99m, 0, 0, halloween, "halloween,pumpkin-patch,field,harvest"),
            P("Ghost Town Sticker", "Abandoned western ghost town.", 3.49m, 15, 245, halloween, "halloween,ghost-town,abandoned,western"),
            P("Monster Eyes Sticker", "Glowing monster eyes in dark.", 2.49m, 0, 295, halloween, "halloween,monster,eyes,glowing"),
            P("Vampire Cape Sticker", "Red and black vampire cape.", 2.99m, 0, 270, halloween, "halloween,cape,vampire,red"),
            P("Spider Sticker", "Black hairy spider.", 2.49m, 10, 305, halloween, "halloween,spider,black,creepy"),
            P("Fog Mist Sticker", "Creepy ground fog effect.", 2.99m, 0, 280, halloween, "halloween,fog,mist,atmosphere"),
            P("Devil Horns Sticker", "Red devil horns and tail.", 2.49m, 0, 290, halloween, "halloween,devil,horns,demon"),
            P("Ouija Board Sticker", "Mystical ouija board.", 3.49m, 20, 240, halloween, "halloween,ouija,board,spirits"),
            P("Raven Bird Sticker", "Black raven perched.", 2.99m, 0, 265, halloween, "halloween,raven,bird,black"),
            P("Candelabra Sticker", "Gothic candelabra with candles.", 3.49m, 0, 250, halloween, "halloween,candelabra,candles,gothic"),
            P("Potion Bottles Sticker", "Row of colorful potion bottles.", 2.99m, 15, 270, halloween, "halloween,potion,bottles,colorful"),
            P("Gargoyle Sticker", "Stone gargoyle statue.", 3.49m, 0, 245, halloween, "halloween,gargoyle,statue,stone"),
            P("Witch Face Sticker", "Ugly witch with wart.", 2.49m, 0, 285, halloween, "halloween,witch,face,ugly"),
            P("Goblet Sticker", "Medieval goblet with red wine.", 2.99m, 10, 260, halloween, "halloween,goblet,wine,medieval"),
            P("Bloody Handprint Sticker", "Red bloody handprint.", 2.49m, 0, 290, halloween, "halloween,blood,handprint,scary"),
            P("Pentagram Sticker", "Mystical five-pointed star.", 2.99m, 0, 275, halloween, "halloween,pentagram,star,occult"),
            P("Demon Mask Sticker", "Scary demon mask.", 3.49m, 15, 235, halloween, "halloween,demon,mask,scary"),
            P("Lightning Bolt Sticker", "Yellow lightning strike.", 2.49m, 0, 295, halloween, "halloween,lightning,bolt,storm"),
            P("Skull Crossbones Sticker", "Skull and crossbones pirate.", 2.99m, 0, 270, halloween, "halloween,skull,crossbones,pirate"),
            P("Crystal Ball Sticker", "Fortune teller crystal ball.", 3.49m, 10, 250, halloween, "halloween,crystal-ball,fortune,mystic"),
            P("Haunted Mirror Sticker", "Cracked antique mirror.", 2.99m, 0, 265, halloween, "halloween,mirror,haunted,cracked"),
            P("Clown Mask Sticker", "Creepy circus clown mask.", 2.49m, 0, 285, halloween, "halloween,clown,mask,circus"),
            P("Voodoo Doll Sticker", "Stitched voodoo doll with pins.", 2.99m, 20, 15, halloween, "halloween,voodoo,doll,pins"),
            P("Owl Night Sticker", "Wise owl perched at night.", 2.49m, 0, 280, halloween, "halloween,owl,night,wise"),
            P("Chainsaw Sticker", "Horror movie chainsaw.", 3.49m, 0, 230, halloween, "halloween,chainsaw,horror,weapon"),
            P("Bubbling Cauldron Sticker", "Green bubbles from cauldron.", 2.99m, 15, 255, halloween, "halloween,cauldron,bubbles,potion"),
            P("Grim Reaper Sticker", "Death with scythe and hood.", 3.99m, 0, 220, halloween, "halloween,grim-reaper,death,scythe"),
            P("Vampire Castle Sticker", "Dark Gothic castle on hill.", 4.49m, 0, 200, halloween, "halloween,castle,gothic,vampire"),
            P("Witch Brew Sticker", "Smoking green witch brew.", 2.99m, 10, 260, halloween, "halloween,brew,witch,smoking"),
            P("Masked Figure Sticker", "Horror movie masked killer.", 3.49m, 0, 240, halloween, "halloween,mask,horror,killer"),
            P("Creepy Doll Sticker", "Possessed antique doll.", 2.99m, 0, 0, halloween, "halloween,doll,creepy,possessed"),
            P("Blood Splatter Sticker", "Red blood splatter effect.", 2.49m, 15, 275, halloween, "halloween,blood,splatter,gore"),
            P("Possessed Eyes Sticker", "White glowing possessed eyes.", 2.49m, 0, 280, halloween, "halloween,possessed,eyes,demonic"),
            P("Horror Hand Sticker", "Skeletal hand reaching.", 2.99m, 0, 265, halloween, "halloween,hand,skeletal,reaching"),
            P("Dark Forest Sticker", "Misty dark forest path.", 3.49m, 10, 245, halloween, "halloween,forest,dark,misty"),
            P("Scary Smile Sticker", "Sinister wide grin.", 2.49m, 0, 270, halloween, "halloween,smile,sinister,scary"),
            P("Shadow Figure Sticker", "Dark shadowy silhouette.", 2.99m, 0, 255, halloween, "halloween,shadow,figure,dark"),
            P("Plague Doctor Sticker", "Medieval plague doctor mask.", 3.49m, 20, 0, halloween, "halloween,plague-doctor,mask,medieval"),

            // ══════════════════════════════════════════════════════════════
            // LUNAR NEW YEAR (39 products)
            // ══════════════════════════════════════════════════════════════
            P("Red Envelope Sticker", "Lucky red money envelope.", 2.49m, 0, 220, lunarNY, "lunar-new-year,red-envelope,money,lucky"),
            P("Dragon Dance Sticker", "Traditional Chinese dragon.", 3.99m, 15, 180, lunarNY, "lunar-new-year,dragon,dance,chinese"),
            P("Firecracker Sticker", "Red firecrackers exploding.", 2.99m, 0, 200, lunarNY, "lunar-new-year,firecracker,explosion,celebration"),
            P("Zodiac Rat Sticker", "Year of the Rat symbol.", 2.49m, 0, 215, lunarNY, "lunar-new-year,zodiac,rat,animal"),
            P("Zodiac Ox Sticker", "Year of the Ox symbol.", 2.49m, 10, 210, lunarNY, "lunar-new-year,zodiac,ox,animal"),
            P("Zodiac Tiger Sticker", "Year of the Tiger symbol.", 2.49m, 0, 205, lunarNY, "lunar-new-year,zodiac,tiger,animal"),
            P("Zodiac Rabbit Sticker", "Year of the Rabbit symbol.", 2.49m, 0, 215, lunarNY, "lunar-new-year,zodiac,rabbit,animal"),
            P("Zodiac Dragon Sticker", "Year of the Dragon symbol.", 2.99m, 15, 0, lunarNY, "lunar-new-year,zodiac,dragon,animal"),
            P("Zodiac Snake Sticker", "Year of the Snake symbol.", 2.49m, 0, 200, lunarNY, "lunar-new-year,zodiac,snake,animal"),
            P("Zodiac Horse Sticker", "Year of the Horse symbol.", 2.49m, 0, 205, lunarNY, "lunar-new-year,zodiac,horse,animal"),
            P("Zodiac Goat Sticker", "Year of the Goat symbol.", 2.49m, 10, 195, lunarNY, "lunar-new-year,zodiac,goat,animal"),
            P("Zodiac Monkey Sticker", "Year of the Monkey symbol.", 2.49m, 0, 210, lunarNY, "lunar-new-year,zodiac,monkey,animal"),
            P("Zodiac Rooster Sticker", "Year of the Rooster symbol.", 2.49m, 0, 200, lunarNY, "lunar-new-year,zodiac,rooster,animal"),
            P("Zodiac Dog Sticker", "Year of the Dog symbol.", 2.49m, 15, 15, lunarNY, "lunar-new-year,zodiac,dog,animal"),
            P("Zodiac Pig Sticker", "Year of the Pig symbol.", 2.49m, 0, 205, lunarNY, "lunar-new-year,zodiac,pig,animal"),
            P("Lion Dance Sticker", "Traditional lion dance costume.", 3.49m, 0, 175, lunarNY, "lunar-new-year,lion,dance,tradition"),
            P("Mandarin Orange Sticker", "Lucky mandarin orange pair.", 2.49m, 10, 220, lunarNY, "lunar-new-year,orange,fruit,lucky"),
            P("Chinese Lantern Sticker", "Red paper lantern hanging.", 2.99m, 0, 195, lunarNY, "lunar-new-year,lantern,red,decoration"),
            P("Fu Character Sticker", "Fortune blessing character.", 2.49m, 0, 210, lunarNY, "lunar-new-year,fu,fortune,blessing"),
            P("Dumpling Sticker", "Steamed Chinese dumplings.", 2.49m, 15, 205, lunarNY, "lunar-new-year,dumpling,food,chinese"),
            P("Cherry Blossom Sticker", "Pink cherry blossoms blooming.", 2.99m, 0, 185, lunarNY, "lunar-new-year,cherry-blossom,flower,spring"),
            P("Spring Couplet Sticker", "Red door couplet banner.", 2.49m, 0, 200, lunarNY, "lunar-new-year,couplet,banner,door"),
            P("Gold Ingot Sticker", "Traditional gold ingot wealth.", 2.99m, 10, 180, lunarNY, "lunar-new-year,gold,ingot,wealth"),
            P("Koi Fish Sticker", "Lucky koi fish swimming.", 3.49m, 0, 165, lunarNY, "lunar-new-year,koi,fish,lucky"),
            P("Temple Gate Sticker", "Chinese temple entrance gate.", 3.99m, 0, 150, lunarNY, "lunar-new-year,temple,gate,chinese"),
            P("Tea Set Sticker", "Traditional Chinese tea set.", 3.49m, 15, 160, lunarNY, "lunar-new-year,tea,set,tradition"),
            P("Bamboo Sticker", "Lucky bamboo stalks.", 2.99m, 0, 185, lunarNY, "lunar-new-year,bamboo,lucky,plant"),
            P("Peony Flower Sticker", "Beautiful pink peony bloom.", 2.99m, 0, 190, lunarNY, "lunar-new-year,peony,flower,pink"),
            P("Fan Folding Sticker", "Decorative Chinese folding fan.", 2.49m, 10, 200, lunarNY, "lunar-new-year,fan,folding,decoration"),
            P("Plum Blossom Sticker", "Delicate plum blossom branch.", 2.99m, 0, 180, lunarNY, "lunar-new-year,plum,blossom,flower"),
            P("Lucky Knot Sticker", "Chinese decorative knot.", 2.49m, 0, 205, lunarNY, "lunar-new-year,knot,lucky,decoration"),
            P("Prosperity Symbol Sticker", "Chinese prosperity character.", 2.49m, 15, 195, lunarNY, "lunar-new-year,prosperity,symbol,character"),
            P("Moon Cake Sticker", "Traditional mooncake pastry.", 2.99m, 0, 175, lunarNY, "lunar-new-year,mooncake,pastry,tradition"),
            P("Jade Pendant Sticker", "Lucky green jade pendant.", 3.49m, 0, 155, lunarNY, "lunar-new-year,jade,pendant,lucky"),
            P("Nian Beast Sticker", "Mythical Nian monster creature.", 2.99m, 10, 170, lunarNY, "lunar-new-year,nian,beast,mythical"),
            P("Rice Cake Sticker", "Traditional sticky rice cake.", 2.49m, 0, 195, lunarNY, "lunar-new-year,rice-cake,food,sticky"),
            P("Phoenix Bird Sticker", "Mythical phoenix rising.", 3.99m, 0, 140, lunarNY, "lunar-new-year,phoenix,bird,mythical"),
            P("Incense Sticks Sticker", "Burning incense sticks.", 2.99m, 15, 165, lunarNY, "lunar-new-year,incense,burning,tradition"),
            P("Blessing Circle Sticker", "Circular blessing design.", 2.49m, 0, 190, lunarNY, "lunar-new-year,blessing,circle,design"),

            // ══════════════════════════════════════════════════════════════
            // NATURE & FLORAL (24 products)
            // ══════════════════════════════════════════════════════════════
            P("Rose Red Sticker", "Classic red rose bloom.", 2.99m, 0, 200, nature, "nature,rose,flower,red"),
            P("Sunflower Sticker", "Bright yellow sunflower.", 2.49m, 15, 220, nature, "nature,sunflower,yellow,flower"),
            P("Lavender Sprig Sticker", "Purple lavender stems.", 2.99m, 0, 190, nature, "nature,lavender,purple,flower"),
            P("Fern Leaf Sticker", "Green fern frond.", 2.49m, 0, 210, nature, "nature,fern,leaf,green"),
            P("Daisy Flower Sticker", "White daisy with yellow center.", 2.49m, 10, 215, nature, "nature,daisy,white,flower"),
            P("Eucalyptus Branch Sticker", "Silver eucalyptus leaves.", 2.99m, 0, 185, nature, "nature,eucalyptus,leaf,branch"),
            P("Wildflower Mix Sticker", "Colorful wildflower bouquet.", 3.49m, 0, 165, nature, "nature,wildflower,bouquet,colorful"),
            P("Succulent Plant Sticker", "Green succulent rosette.", 2.49m, 15, 200, nature, "nature,succulent,plant,green"),
            P("Lotus Flower Sticker", "Pink lotus bloom on water.", 3.49m, 0, 170, nature, "nature,lotus,flower,pink"),
            P("Mountain Range Sticker", "Snow-capped mountain peaks.", 3.99m, 0, 150, nature, "nature,mountain,peak,landscape"),
            P("Ocean Wave Sticker", "Crashing blue ocean wave.", 2.99m, 10, 180, nature, "nature,ocean,wave,blue"),
            P("Forest Tree Sticker", "Tall evergreen pine tree.", 2.49m, 0, 205, nature, "nature,tree,forest,evergreen"),
            P("Cactus Desert Sticker", "Green saguaro cactus.", 2.99m, 0, 190, nature, "nature,cactus,desert,succulent"),
            P("Leaf Skeleton Sticker", "Delicate leaf vein pattern.", 2.49m, 15, 195, nature, "nature,leaf,skeleton,pattern"),
            P("Mushroom Forest Sticker", "Red and white spotted mushroom.", 2.99m, 0, 175, nature, "nature,mushroom,forest,fungi"),
            P("Waterfall Sticker", "Cascading waterfall stream.", 3.49m, 0, 160, nature, "nature,waterfall,stream,cascade"),
            P("Bee Pollinating Sticker", "Bee on sunflower.", 2.49m, 10, 200, nature, "nature,bee,pollinate,sunflower"),
            P("Autumn Leaf Sticker", "Orange maple leaf.", 2.49m, 0, 210, nature, "nature,leaf,autumn,maple"),
            P("River Stream Sticker", "Peaceful flowing stream.", 2.99m, 0, 0, nature, "nature,river,stream,water"),
            P("Dandelion Seed Sticker", "Dandelion seeds blowing.", 2.49m, 15, 190, nature, "nature,dandelion,seed,blow"),
            P("Ivy Vine Sticker", "Green ivy climbing vine.", 2.99m, 0, 175, nature, "nature,ivy,vine,climbing"),
            P("Acorn Oak Sticker", "Brown oak acorn.", 2.49m, 0, 195, nature, "nature,acorn,oak,nut"),
            P("Pine Cone Sticker", "Pinecone from evergreen.", 2.49m, 10, 185, nature, "nature,pinecone,pine,cone"),
            P("Palm Leaf Sticker", "Tropical palm frond.", 2.99m, 0, 180, nature, "nature,palm,leaf,tropical"),

            // ══════════════════════════════════════════════════════════════
            // NEW YEARS EVE (30 products)
            // ══════════════════════════════════════════════════════════════
            P("Champagne Toast Sticker", "Two champagne glasses clinking.", 2.99m, 0, 220, newYear, "new-year,champagne,toast,celebrate"),
            P("Fireworks Burst Sticker", "Colorful fireworks explosion.", 2.49m, 15, 240, newYear, "new-year,fireworks,burst,celebration"),
            P("Midnight Clock Sticker", "Clock striking midnight.", 2.99m, 0, 210, newYear, "new-year,clock,midnight,countdown"),
            P("Confetti Sticker", "Colorful confetti pieces.", 1.99m, 0, 260, newYear, "new-year,confetti,colorful,party"),
            P("Party Hat Sticker", "Cone party hat with streamers.", 2.49m, 10, 235, newYear, "new-year,party-hat,celebrate,cone"),
            P("Balloon Bunch Sticker", "Gold and silver balloons.", 2.99m, 0, 215, newYear, "new-year,balloon,gold,silver"),
            P("Champagne Bottle Sticker", "Popping champagne bottle.", 3.49m, 0, 190, newYear, "new-year,champagne,bottle,pop"),
            P("2025 Numbers Sticker", "Gold 2025 year numbers.", 2.99m, 15, 225, newYear, "new-year,2025,numbers,gold"),
            P("Disco Ball Sticker", "Shiny mirror disco ball.", 2.49m, 0, 230, newYear, "new-year,disco-ball,party,mirror"),
            P("Noisemaker Sticker", "Party horn blower.", 1.99m, 0, 255, newYear, "new-year,noisemaker,horn,party"),
            P("Sparkler Sticker", "Lit sparkler sparking.", 2.49m, 10, 240, newYear, "new-year,sparkler,spark,light"),
            P("Cheers Banner Sticker", "Cheers text banner.", 2.99m, 0, 220, newYear, "new-year,cheers,banner,text"),
            P("Countdown Sticker", "321 countdown numbers.", 2.49m, 0, 235, newYear, "new-year,countdown,321,numbers"),
            P("Resolution List Sticker", "New year resolution checklist.", 2.99m, 15, 200, newYear, "new-year,resolution,list,goals"),
            P("Glitter Stars Sticker", "Gold glitter star pattern.", 1.99m, 0, 250, newYear, "new-year,glitter,stars,gold"),
            P("Champagne Glass Sticker", "Single champagne flute.", 2.49m, 0, 230, newYear, "new-year,champagne,glass,flute"),
            P("Party Streamer Sticker", "Curly party streamers.", 2.49m, 10, 225, newYear, "new-year,streamer,party,curly"),
            P("Midnight Skyline Sticker", "City skyline at midnight.", 3.99m, 0, 175, newYear, "new-year,skyline,city,midnight"),
            P("Celebration Stars Sticker", "Bursting star pattern.", 2.49m, 0, 235, newYear, "new-year,stars,burst,celebration"),
            P("Auld Lang Syne Sticker", "Music notes with text.", 2.99m, 15, 205, newYear, "new-year,auld-lang-syne,music,song"),
            P("Times Square Ball Sticker", "New Year ball drop.", 3.49m, 0, 0, newYear, "new-year,ball-drop,times-square,nyc"),
            P("Happy New Year Banner Sticker", "Happy New Year text.", 2.49m, 0, 240, newYear, "new-year,banner,text,happy"),
            P("Top Hat Sticker", "Fancy black top hat.", 2.99m, 10, 210, newYear, "new-year,top-hat,fancy,black"),
            P("Champagne Cork Sticker", "Popped champagne cork.", 1.99m, 0, 245, newYear, "new-year,cork,champagne,pop"),
            P("Party Glasses Sticker", "2025 shaped party glasses.", 2.49m, 0, 225, newYear, "new-year,glasses,party,2025"),
            P("Firework Rocket Sticker", "Firework rocket launching.", 2.99m, 15, 200, newYear, "new-year,rocket,firework,launch"),
            P("Golden Bow Tie Sticker", "Fancy gold bow tie.", 2.49m, 0, 220, newYear, "new-year,bow-tie,gold,fancy"),
            P("Celebration Cake Sticker", "New Year celebration cake.", 3.49m, 0, 185, newYear, "new-year,cake,celebration,dessert"),
            P("Kiss at Midnight Sticker", "Couple kissing at midnight.", 2.99m, 10, 195, newYear, "new-year,kiss,midnight,couple"),
            P("New Beginnings Sticker", "Sunrise with text.", 2.49m, 0, 215, newYear, "new-year,beginnings,sunrise,fresh-start"),

            // ══════════════════════════════════════════════════════════════
            // THANKSGIVING (23 products)
            // ══════════════════════════════════════════════════════════════
            P("Roast Turkey Sticker", "Golden roasted turkey.", 3.99m, 0, 180, thanks, "thanksgiving,turkey,roast,dinner"),
            P("Pumpkin Pie Sticker", "Slice of pumpkin pie.", 2.99m, 15, 200, thanks, "thanksgiving,pie,pumpkin,dessert"),
            P("Cornucopia Sticker", "Horn of plenty with harvest.", 3.49m, 0, 170, thanks, "thanksgiving,cornucopia,harvest,plenty"),
            P("Fall Leaves Sticker", "Orange and red autumn leaves.", 2.49m, 0, 210, thanks, "thanksgiving,leaves,autumn,fall"),
            P("Pilgrim Hat Sticker", "Black pilgrim hat with buckle.", 2.99m, 10, 185, thanks, "thanksgiving,pilgrim,hat,buckle"),
            P("Thankful Text Sticker", "Thankful handwritten text.", 2.49m, 0, 205, thanks, "thanksgiving,thankful,text,grateful"),
            P("Harvest Wheat Sticker", "Golden wheat stalks bundle.", 2.99m, 0, 190, thanks, "thanksgiving,wheat,harvest,grain"),
            P("Cranberry Sauce Sticker", "Can of cranberry sauce.", 2.49m, 15, 195, thanks, "thanksgiving,cranberry,sauce,can"),
            P("Mashed Potatoes Sticker", "Bowl of creamy mashed potatoes.", 2.99m, 0, 180, thanks, "thanksgiving,mashed-potatoes,side,bowl"),
            P("Gravy Boat Sticker", "Gravy boat pouring.", 2.49m, 0, 200, thanks, "thanksgiving,gravy,boat,sauce"),
            P("Stuffing Dish Sticker", "Baked stuffing in dish.", 2.99m, 10, 175, thanks, "thanksgiving,stuffing,dish,baked"),
            P("Green Bean Casserole Sticker", "Classic green bean casserole.", 3.49m, 0, 160, thanks, "thanksgiving,green-beans,casserole,side"),
            P("Dinner Table Sticker", "Set Thanksgiving dinner table.", 3.99m, 0, 0, thanks, "thanksgiving,table,dinner,setting"),
            P("Turkey Wishbone Sticker", "Lucky turkey wishbone.", 2.49m, 15, 190, thanks, "thanksgiving,wishbone,turkey,lucky"),
            P("Acorn Sticker", "Brown oak acorn.", 1.99m, 0, 220, thanks, "thanksgiving,acorn,oak,autumn"),
            P("Give Thanks Sticker", "Give Thanks banner text.", 2.49m, 0, 205, thanks, "thanksgiving,give-thanks,banner,text"),
            P("Apple Cider Sticker", "Mug of hot apple cider.", 2.99m, 10, 185, thanks, "thanksgiving,cider,apple,drink"),
            P("Autumn Wreath Sticker", "Fall foliage door wreath.", 3.49m, 0, 165, thanks, "thanksgiving,wreath,autumn,door"),
            P("Scarecrow Fall Sticker", "Friendly scarecrow in field.", 2.99m, 0, 180, thanks, "thanksgiving,scarecrow,fall,field"),
            P("Corn on Cob Sticker", "Buttered corn on cob.", 2.49m, 15, 195, thanks, "thanksgiving,corn,cob,butter"),
            P("Grateful Heart Sticker", "Heart with grateful text.", 2.49m, 0, 200, thanks, "thanksgiving,grateful,heart,text"),
            P("Family Gathering Sticker", "Family around dinner table.", 3.99m, 0, 150, thanks, "thanksgiving,family,gathering,together"),
            P("Pecan Pie Sticker", "Slice of pecan pie.", 2.99m, 10, 175, thanks, "thanksgiving,pie,pecan,dessert"),

            // ══════════════════════════════════════════════════════════════
            // MISCELLANEOUS (11 products)
            // ══════════════════════════════════════════════════════════════
            P("Rainbow Pride Sticker", "Colorful rainbow pride arc.", 2.49m, 0, 240, misc, "miscellaneous,rainbow,pride,lgbtq"),
            P("Retro Sunset Sticker", "80s style sunset stripes.", 2.99m, 15, 220, misc, "miscellaneous,retro,sunset,80s"),
            P("Peace Sign Sticker", "Hand peace sign symbol.", 2.49m, 0, 235, misc, "miscellaneous,peace,sign,hand"),
            P("Music Note Sticker", "Black music note symbol.", 1.99m, 0, 250, misc, "miscellaneous,music,note,symbol"),
            P("Lightning Flash Sticker", "Yellow lightning bolt.", 2.49m, 10, 230, misc, "miscellaneous,lightning,flash,bolt"),
            P("Moon Phases Sticker", "Lunar phase cycle.", 2.99m, 0, 210, misc, "miscellaneous,moon,phases,lunar"),
            P("Compass Rose Sticker", "Vintage compass design.", 2.49m, 0, 225, misc, "miscellaneous,compass,rose,navigation"),
            P("Om Symbol Sticker", "Sacred Om meditation symbol.", 2.99m, 15, 200, misc, "miscellaneous,om,meditation,spiritual"),
            P("Yin Yang Sticker", "Black and white balance symbol.", 2.49m, 0, 220, misc, "miscellaneous,yin-yang,balance,symbol"),
            P("Infinity Symbol Sticker", "Gold infinity loop.", 2.99m, 0, 0, misc, "miscellaneous,infinity,loop,symbol"),
            P("Good Vibes Sticker", "Good vibes only text.", 2.49m, 10, 230, misc, "miscellaneous,good-vibes,positive,text"),
        };
    }
}