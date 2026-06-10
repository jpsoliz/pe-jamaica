# PRD Addendum

## Architecture-Depth Context

### ArcGIS Enterprise Processing Option

The user clarified that v1 should consider ArcGIS Enterprise Server as part of the system boundary, not only local ArcGIS Pro execution. Specifically, v1 should explore whether some processing can run as ArcGIS Enterprise services:

- ArcGIS Enterprise Web Tool / geoprocessing service.
- ArcGIS Notebooks Advanced / Notebook Server for Python and ArcPy-backed processes.
- Enterprise-hosted data and potentially service-backed workflows.

Research verification:

- ArcGIS Enterprise web tools/geoprocessing services allow analysis to be shared through Portal/ArcGIS Server, with processing occurring on the server and clients able to run the same tool concurrently.
- ArcGIS Notebooks are Jupyter-based Python notebooks available in ArcGIS Enterprise through Notebook Server; documented environments can access ArcGIS API for Python and ArcPy.

PRD implication:

- The PRD should not assume all processing must remain local in ArcGIS Pro.
- The v1 scope should include an explicit decision point for which workflow steps stay in the ArcGIS Pro add-in versus which are candidates for Enterprise-hosted geoprocessing or notebook-backed services.
- The PRD should still preserve ArcGIS Pro as the primary operator experience for cadastral officials unless discovery determines a different user workflow.

Sources:

- https://enterprise.arcgis.com/en/server/11.5/publish-services/windows/what-is-a-web-tool.htm
- https://enterprise.arcgis.com/en/server/11.5/install/windows/service-publishing-in-arcgis-pro.htm
- https://architecture.arcgis.com/en/framework/architecture-pillars/integration/methods/arcgis-notebooks.html
