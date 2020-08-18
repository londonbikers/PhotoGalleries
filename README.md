# PhotoGalleries

A website to showcase the https://londonbikers.com photo galleries, both old and new. Currently in development.

## Planned tech stack:

* ASP.NET Core MVC
* Azure Cosmos DB
* Azure Blob storage
* Azure CDN
* ImageFlow (dynamic image resizing - https://www.imageflow.io)
* Azure Web App hosting

## Planned features:

* Photo galleries
* Gallery categories
* Photom metadata display, i.e. camera settings
* Tag extraction from metadata
* Tag generation via Azure Cognitive Services
* Discourse API integration to allow cross-posting onto londonbikers.com
* Photo user comments
* Gallery user comments
* OpenID Connect federation with our IdentityServer IDP

Photos will need migrating from both our archive and the current LB photos website.

## Website Structure

| path | description |
|---|---|
|/|latest galleries + intro|
|/categories|list of categories|
|/categories/{name}|category detail page|
|/galleries/{id}/{name}|gallery detail|
|/admin|admin landing page|
|/admin/categories|categories list|
|/admin/categories/new|create a new category|
|/admin/categories/{id}|category detail page|
|/admin/galleries|gallery list|
|/admin/galleries/new|create a new gallery|
|/admin/galleries/{id}|gallery detail|
