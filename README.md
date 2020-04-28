# Schafkopf
This is an Open Source implementation of the bavarian card game **Schafkopf**.

Feel free to check out the demo at: https://schafkopf.p4u1.de

Or run it on your own server with docker: `docker run -p 9080:80 -p 9443:443 thielepaul/schafkopf`

What can this app offer you:
* Play Schafkopf with friends in their browser
* No logins, no registration, no ads
* It's Open Source: feel free to adapt it to your needs
* No data is stored permanently on the server

Note, that this is a German game so everything in the game is in German.

## Features
* Sauspiel
* Farbsolo
* Wenz
* Hochzeit
* Ramsch
* Chat
* More than 4 Players (additional players can spectate if not playing)

## Screenshots

![screenshot of app in light mode](screenshots/light.png "Light Mode")

![screenshot of app in dark mode](screenshots/dark.png "Dark Mode")

## Development
This is a .NET core project, check out https://dotnet.microsoft.com/download for more information about .NET core.
If you want to play this on a single computer during development, append `&session=new` to the URL to create a new session instead of reconnecting to an existing one.
