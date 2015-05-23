# Ellipsoidus

This software is intended to generate outer limit of Polish *territorial sea*  and *contiguous zone* borders. It follows   [United Nations Convention on the Law of the Sea](http://www.un.org/depts/los/convention_agreements/convention_overview_convention.htm) regulations and aims  to deliver the most accurate results (up to 1 mm). To avoid cartographic projection distorions all calculation are made on surface of WGS-84 ellipsoid whithout using any projection.  

Ellipsoidus calculates lines every point of which is at specyfied distance from the nearest point of the marine *baseline*. Calculation results can be exported to text files along with ArcGIS MXD project with ShapeFiles (SHP). During export generalization precision could be specyfied up to 1 mm.

* See it in action on [YouTube](https://www.youtube.com/playlist?list=PL6ZtrotaJvdaTUcoXuyhNHX9XHFFO7nLr)
* Read [Ellipsoidus Wiki](https://github.com/kubaszostak/ellipsoidus/wiki)

This program is not a competition for professional software like *Caris LOTS* that can do everything and even more. Ellipsoidus is dedicated to do only one thing (generate offset line on ellipsoid surface) and it do it in the most effective way.

### Download
https://github.com/kubaszostak/ellipsoidus/releases


### Requirements
* [Microsoft .NET Framework 4.5](http://www.microsoft.com/net/downloads)

### Credits
* Mapping API provided by Esri [ArcGIS Runtime SDK for .NET](https://developers.arcgis.com/net/)
* Algorithms for solving geodesic problems provided by [Charles Karney's GeographicLib](http://geographiclib.sourceforge.net/)  
* Shapefiles support provided by [DotSpatial](http://dotspatial.codeplex.com/)
* Countries Shapefile provided by [thematicmapping.org](http://thematicmapping.org/)
