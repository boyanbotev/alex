The Game Scene's TurnManager references test_export.json as its Ending Story.
Unity reads that compiled JSON; after editing test.ink, export it from Inky again
and replace test_export.json. No Ink source compiler is installed in Unity.

Declare recognised pairs in LIST city_links. Keys use trimmed, lowercase city
names with spaces replaced by underscores, ordered with ordinal comparison:
Chemistry + Mr Nealen becomes chemistry_mr_nealen. The sample declares all pairs
from the current CityData assets. Add new pairs when you add cities.

EXTERNAL get_links() returns an Ink LIST containing only active bonds across the
map at game over. A missing pair logs a warning and is omitted. The snapshot is
made once; later external calls read it without scanning the board again.

Example:

    ~ temp links = get_links()
    {links ? city_links.chemistry_mr_nealen:
        Write this pair's ending here.
    }

The game ends on human elimination (no cities or units remaining) or when at most
one player survives. TurnManager.EndGame(bool won) is the shared entry point for
a future turn limit. Gameplay remains stopped while the scrollable ending is
shown; Ink choices are supported, with Main menu offered when the story finishes.

Other declared externals (get_depression, get_love, get_rebellion) are not bound
yet. They need implementations before the exported story can call them.
