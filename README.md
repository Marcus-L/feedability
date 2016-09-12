feedability
===========
![Screenshot](/screenshot.jpg)

About
-----
Feedability is a stand-alone ASP.NET Core website and can be hosted in IIS or with the `dotnet` command. Its features include:
* A web interface for testing sites (articles) for simplification with the Readability library
* A web interface for viewing RSS/Atom feeds with feed entry contents replaced with the Readability-processed versions of the entry links. 
* A web service for processing feeds, consumable by RSS readers such as Feedly and cached per-feed
* A web service for returning readable article HTML and managing the feed cache

Usage
-----
* Build & Publish, build output is in src\Feedability\bin\Release\PublishOutput
* __Adjust appsettings.json to appropriate IP Rate Limiting (or disable entirely).__ This is mainly to prevent people from using the demo site as a real feed generator
* Run the web interface which will provide UI for testing the Web API (either by hosting in IIS with the [.NET Core Windows Server Hosting bundle](https://go.microsoft.com/fwlink/?LinkId=817246) or via the `dotnet Feedability.dll` command)
* Test article Readability output (adjust white/blacklist css selectors) and Feed output
* Provide the feed link to the RSS reader

Demo/Examples (current as of 9/8/2016)
--------
Demo|Site|Feed Url|Whitelist|Blacklist
---|---|---|---|---
[Link](http://m4rc.us/sandbox/u/feedability/?method=article&url=https%3A%2F%2Ftechcrunch.com%2F2016%2F09%2F02%2Fgoogles-new-project-muse-proves-machines-arent-that-great-at-fashion-design%2F&whitelist=div.embed-twitter%2C+div.thumbnails&blacklist=div.slideshow+ol)|TechCrunch|https://techcrunch.com/feed|div.embed-twitter, div.thumbnails|div.slideshow ol
[Link](http://m4rc.us/sandbox/u/feedability/?method=article&url=http%3A%2F%2Fphandroid.com%2F2016%2F09%2F08%2Ftwitter-direct-message-update%2F&whitelist=&blacklist=div.ng-scope%2C+div.further-reading-container)|Phandroid|http://phandroid.com/feed||div.ng-scope, div.further-reading-container
[Link](http://m4rc.us/sandbox/u/feedability/?method=article&url=http%3A%2F%2Frapidtravelchai.boardingarea.com%2F2016%2F09%2F01%2Fibizas-curio-hilton%2F&whitelist=.et_social_inline%2C+.et_social_media_wrapper&blacklist=%23jp-relatedposts%2C+.entry-meta%2C+.ba-disclosure)|Rapid Travel Chai|http://rapidtravelchai.boardingarea.com|.et_social_inline, .et_social_media_wrapper|#jp-relatedposts, .entry-meta, .ba-disclosure

Tech Stack
----------
The server side is built using:
* [mozilla readability](https://github.com/mozilla/readability) - a fork of the original Arc90 readability javascript library
* [PhantomJS](http://phantomjs.org/) - a headless web browser to fetch web pages and run readability
* [Sqlite](https://www.sqlite.org/) and the [Microsoft.Data.Sqlite](https://github.com/aspnet/Microsoft.Data.Sqlite) .NET core interface for caching processed feeds
* [AspNetCoreRateLimit](https://github.com/stefanprodan/AspNetCoreRateLimit) - intentionally rate limiting the server to prevent abuse

The client side uses:
* [Polymer Project](https://www.polymer-project.org/1.0/) - web components for UI and material design

External Licenses
-----------------
* [mozilla readability](https://github.com/mozilla/readability) Copyright (c) 2010 Arc90 Inc - Apache License, Version 2.0
* [readable-proxy](https://github.com/n1k0/readable-proxy/) : MPL 2.0.

License
-------
Feedability: ASP.NET Core Full-Feed Readability Proxy

Copyright (c) 2016, Marcus Lum

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program.  If not, see <"http://www.gnu.org/licenses/":http://www.gnu.org/licenses/>.
