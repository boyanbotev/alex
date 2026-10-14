EXTERNAL get_links()
EXTERNAL get_depression()
EXTERNAL get_conscience()

LIST city_links = alex_pudding_bowl_cut, lazy_pudding_bowl_cut, alex_loser, alex_lazy, lazy_loser, loser_pudding_bowl_cut

-> start

== start
~ temp links = get_links()
~ temp depression = get_depression()
~ temp conscience = get_conscience()
{
-depression > conscience:
A kind of malaise settled over Alex. The school day began but Alex no longer listened to the wittering of teachers. He wanted to be in bed.
-depression < conscience:
A kind of malaise started to settle over Alex, but then he suddenly thought 'what am I down about?' And then weirdly he felt fine.
-depression == conscience:
A kind of malaise setlled over Alex but something in him resisted it. The school day had begun, half of Alex was closed down but half was conscious to the wittering voices around.
}
{links ? city_links.alex_loser:
He was a loser, there was no getting around it.
}
{links ? alex_pudding_bowl_cut: 
Alex had a pudding bowl cut.
}
{links ? city_links.loser_pudding_bowl_cut:
Alex's haircut, a pudding bowl cut, was pretty loserish.
}
{links ? city_links.alex_lazy: 
Alex knew he was lazy, but he didn't care.
}
{links ? city_links.lazy_loser:
Alex was a lazy loser. 
}

-> DONE
