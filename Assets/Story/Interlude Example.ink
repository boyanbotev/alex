// Placeholder interlude after Level 1. Replace this dialogue during writing.
EXTERNAL set_flag(key, value)
EXTERNAL set_relation(a, b, relation)

Can we call a truce before we meet again? # speaker: Malaise
* [Let's call a truce.]
    ~ set_flag("accepted_truce", true)
    ~ set_relation("conscience", "malaise", "peace")
    Agreed. We'll be at peace next time. # speaker: Malaise
    -> DONE
* [Not this time.]
    ~ set_flag("accepted_truce", false)
    ~ set_relation("conscience", "malaise", "war")
    Understood. We'll talk again. # speaker: Malaise
    -> DONE
