from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.base_request_builder import BaseRequestBuilder
from kiota_abstractions.base_request_configuration import RequestConfiguration
from kiota_abstractions.default_query_parameters import QueryParameters
from kiota_abstractions.get_path_parameters import get_path_parameters
from kiota_abstractions.method import Method
from kiota_abstractions.request_adapter import RequestAdapter
from kiota_abstractions.request_information import RequestInformation
from kiota_abstractions.request_option import RequestOption
from kiota_abstractions.serialization import Parsable, ParsableFactory
from typing import Any, Optional, TYPE_CHECKING, Union
from warnings import warn

if TYPE_CHECKING:
    from ...models.qyl.common.errors.internal_server_error import InternalServerError
    from .item.with_metric_name_item_request_builder import WithMetricNameItemRequestBuilder
    from .metrics_get_response import MetricsGetResponse
    from .query.query_request_builder import QueryRequestBuilder

class MetricsRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/metrics
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new MetricsRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/metrics{?cursor,limit,namePattern,serviceName}", path_parameters)
    
    def by_metric_name(self,metric_name: str) -> WithMetricNameItemRequestBuilder:
        """
        Gets an item from the ApiSdk.v1.metrics.item collection
        param metric_name: Unique identifier of the item
        Returns: WithMetricNameItemRequestBuilder
        """
        if metric_name is None:
            raise TypeError("metric_name cannot be null.")
        from .item.with_metric_name_item_request_builder import WithMetricNameItemRequestBuilder

        url_tpl_params = get_path_parameters(self.path_parameters)
        url_tpl_params["metricName"] = metric_name
        return WithMetricNameItemRequestBuilder(self.request_adapter, url_tpl_params)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[MetricsRequestBuilderGetQueryParameters]] = None) -> Optional[MetricsGetResponse]:
        """
        List available metrics
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[MetricsGetResponse]
        """
        request_info = self.to_get_request_information(
            request_configuration
        )
        from ...models.qyl.common.errors.internal_server_error import InternalServerError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from .metrics_get_response import MetricsGetResponse

        return await self.request_adapter.send_async(request_info, MetricsGetResponse, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[MetricsRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        List available metrics
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def with_url(self,raw_url: str) -> MetricsRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: MetricsRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return MetricsRequestBuilder(self.request_adapter, raw_url)
    
    @property
    def query(self) -> QueryRequestBuilder:
        """
        The query property
        """
        from .query.query_request_builder import QueryRequestBuilder

        return QueryRequestBuilder(self.request_adapter, self.path_parameters)
    
    @dataclass
    class MetricsRequestBuilderGetQueryParameters():
        """
        List available metrics
        """
        def get_query_parameter(self,original_name: str) -> str:
            """
            Maps the query parameters names to their encoded names for the URI template parsing.
            param original_name: The original query parameter name in the class.
            Returns: str
            """
            if original_name is None:
                raise TypeError("original_name cannot be null.")
            if original_name == "name_pattern":
                return "namePattern"
            if original_name == "service_name":
                return "serviceName"
            if original_name == "cursor":
                return "cursor"
            if original_name == "limit":
                return "limit"
            return original_name
        
        # Cursor
        cursor: Optional[str] = None

        # Page size
        limit: Optional[int] = None

        # Metric name pattern
        name_pattern: Optional[str] = None

        # Service name filter
        service_name: Optional[str] = None

    
    @dataclass
    class MetricsRequestBuilderGetRequestConfiguration(RequestConfiguration[MetricsRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

