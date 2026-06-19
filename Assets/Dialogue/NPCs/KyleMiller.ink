Ughh. this race is so BORING.
It's been okay.. #player
We NEED a late race caution!
Yeah, that would be exciting.. #player
What if WE caused one?
Huh? How would we even do that? #player
Throw my beer over the fence!
    + [Okay. I'll do it]
        -> chosen("Accept")
    + [I don't think that will work..]
        -> chosen("Doubt")
    + [No way!]
        -> chosen("Decline")
      
=== chosen(choice) ===
DO IT! I would but I can't throw too good.
{ choice == "Accept": You're on! Get out there and cover some laps first. #quest_start:KyleCaution }
-> END