EXTERNAL get_depression()
EXTERNAL get_conscience()

-> start

== start
~ temp depression = get_depression()
~ temp conscience = get_conscience()
{
-depression > conscience:
A kind of malaise settled over Alex. The school day began but Alex no longer listened to the wittering of teachers. He wanted to be in bed.
-depression < conscience:
A kind of malaise started to settle over Alex, but then he suddenly thought 'what am I down about?' And then weirdly he felt fine.
-depression == conscience:
A kind of malaise settled over Alex but something in him resisted it. The school day had begun, half of Alex was closed down but half was conscious to the wittering voices around.
}

-> DONE
