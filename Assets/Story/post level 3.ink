EXTERNAL set_relation(a, b, relation)



Heyyyy #speaker: Love

*[Hi!]
Hey
*[Do I know you?]
Not yet?
*[Don't tell me... Conscience sent you]
umm what?
No, not at all

- I just saw you're doing Conscience stuff and I wondered if you wanted to maybe hang out?
*[Yeah? Sure]
Oh.. great :)
I thought we could join our armies, make connections between each others' cities, things like that
does that sound ok?
    **[actually I'm good on my own] -> reject
    **[Sounds great!] -> accept
*[Hang out? I'm busy] -> reject

===accept
~ set_relation("conscience", "love", "allied")
Oki (:
-> DONE

===reject
Oh... ok.
Good luck!
->DONE