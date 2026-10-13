# Interludes

Assign exported Ink JSON to a Level's **Interlude Story** field. The story runs after that level's existing ending, before the next level. An empty field keeps the direct next-level transition.

The example after Level 1 is placeholder dialogue for testing. Accepting the truce makes Conscience and Malaise start Level 2 at peace. Rejecting it keeps them at war. Replace the example with authored content when ready; export edited `.ink` files to JSON as with the existing level endings.

## Messages

Each emitted Ink line becomes a message. Add `# speaker: Character name` to identify its sender. Add `# outgoing` to render a scripted player message on the right. Choices appear as reply buttons and the selected reply is added to the history automatically, so use bracketed choice text (`* [Reply]`) to avoid emitting it twice.

Untagged lines retain the previous incoming speaker, including after a player reply and when resuming. Character portraits are configured in the Overworld controller's **Portraits** list, matched to speaker names without regard to case. Conscience is a separate incoming character; player replies remain **You** and have no portrait. Images keep their aspect ratio and do not need Sprite import settings.

## Campaign functions

Declare the external functions used by your story:

```ink
EXTERNAL get_flag(key)
EXTERNAL set_flag(key, value)
EXTERNAL set_relation(a, b, relation)
EXTERNAL get_score(key)
EXTERNAL has_link(key)
```

- `get_flag` returns a boolean, defaulting to false.
- `set_flag` records a persistent boolean.
- `set_relation` records a symmetric persistent faction relationship: `peace`, `war`, or `allied`. Use the **Campaign Id** field on Faction assets. These IDs must be unique and remain stable across renaming assets.
- `get_score` returns a previous-level faction score by its ending function name, defaulting to zero.
- `has_link` checks a previous-level city pair using the same pair keys as the level ending.

Existing `get_links()` and faction ending score functions are also available. To use `get_links()`, declare the appropriate `LIST city_links` in the story, as in the existing endings.

Relations only apply to factions present in the next match and persist for later matches. Runtime decisions never modify Level or Faction assets. In-match diplomatic actions currently stay local to the match; only explicit campaign effects persist.

## Saving and testing

The main menu resumes the saved gameplay/interlude phase. Interludes save Ink execution state, transcript and campaign effects together at reply boundaries and completion. There is no skip button. Gameplay still restarts the saved level; this does not add mid-match saving.

Use **Tools > SaveEditor > Reset campaign** to clear choices and relationships for a fresh playthrough. **Set Level** clears the active interlude but keeps campaign flags and relationships.

Opening Overworld directly without an active saved interlude displays an error with a Main menu button. To test the example, finish Level 1 and choose Continue on its ending.
