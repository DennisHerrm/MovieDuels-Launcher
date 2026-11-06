# What the launcher reads online

Every launcher reads this folder from GitHub when it opens
(`https://raw.githubusercontent.com/MovieDuels/MovieDuels-Launcher/main/online/`). Change a file here, push it
to `main`, and players see it the next time they open the launcher. GitHub can take a few minutes to show the
new version.

## latest.json: a new update

```json
{
  "update": 9,
  "title": "Update 9",
  "whatsNew": [ "New saber effects and dismemberment", "New gunner moves and sounds" ],
  "moddb": "https://www.moddb.com/mods/movie-duels/downloads",
  "nexus": "https://www.nexusmods.com/starwarsjediknightjediacademy/mods/45?tab=files",
  "article": "update-9"
}
```

- `update`: the number the new release's `ui/main.menu` says (`uiScript "mdUpdate" "9"`). Players with an
  older one see the red emblem. Raise it only when the release is on ModDB / Nexus Mods.
- `whatsNew`: up to six short lines for the emblem's window.
- `moddb`, `nexus`: where its two buttons go (a file page is fine).
- `article`: the `id` of a news article about it (or `null`); its window then has a "What's new" button.

## news.json: the NEWS page

```json
{
  "articles": [
    {
      "id": "update-9",
      "tag": "Update",
      "date": "24 Dec 2026",
      "title": "Update 9 is out!",
      "summary": "One or two lines for the card.",
      "image": "news/update9.jpg",
      "file": "news/update9.html"
    }
  ]
}
```

Newest first. `id` must stay the same once published: the launcher remembers which ids a player has seen, and
the NEWS button flashes for the others. The picture shows at about 548 x 318 on the card (1096 x 636 or a
similar shape looks sharp); JPG keeps it small.

## An article's HTML

Plain HTML without a page around it. The launcher shows these parts and skips everything else (scripts don't
run):

```html
<p>A paragraph with <b>bold</b>, <i>italic</i> and a <a href="https://www.moddb.com/mods/movie-duels">link</a>.</p>
<h3>A heading</h3>
<ul>
  <li>A list</li>
</ul>
<p class="note">A highlighted note.</p>
<img src="news/screenshot.jpg">
```

Links must start with `https://` and open in the browser. Pictures come from this folder (or a full `https`
address).

Check a JSON file before pushing (for example with an online JSON validator): a file the launcher can't read is
treated as if it wasn't there.
