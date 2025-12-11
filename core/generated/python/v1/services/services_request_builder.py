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
    from .item.with_service_name_item_request_builder import WithServiceNameItemRequestBuilder
    from .services_get_response import ServicesGetResponse

class ServicesRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/services
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new ServicesRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/services{?cursor,limit,namespaceName}", path_parameters)
    
    def by_service_name(self,service_name: str) -> WithServiceNameItemRequestBuilder:
        """
        Gets an item from the ApiSdk.v1.services.item collection
        param service_name: Unique identifier of the item
        Returns: WithServiceNameItemRequestBuilder
        """
        if service_name is None:
            raise TypeError("service_name cannot be null.")
        from .item.with_service_name_item_request_builder import WithServiceNameItemRequestBuilder

        url_tpl_params = get_path_parameters(self.path_parameters)
        url_tpl_params["serviceName"] = service_name
        return WithServiceNameItemRequestBuilder(self.request_adapter, url_tpl_params)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[ServicesRequestBuilderGetQueryParameters]] = None) -> Optional[ServicesGetResponse]:
        """
        List discovered services
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[ServicesGetResponse]
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
        from .services_get_response import ServicesGetResponse

        return await self.request_adapter.send_async(request_info, ServicesGetResponse, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[ServicesRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        List discovered services
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def with_url(self,raw_url: str) -> ServicesRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: ServicesRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return ServicesRequestBuilder(self.request_adapter, raw_url)
    
    @dataclass
    class ServicesRequestBuilderGetQueryParameters():
        """
        List discovered services
        """
        def get_query_parameter(self,original_name: str) -> str:
            """
            Maps the query parameters names to their encoded names for the URI template parsing.
            param original_name: The original query parameter name in the class.
            Returns: str
            """
            if original_name is None:
                raise TypeError("original_name cannot be null.")
            if original_name == "namespace_name":
                return "namespaceName"
            if original_name == "cursor":
                return "cursor"
            if original_name == "limit":
                return "limit"
            return original_name
        
        # Cursor
        cursor: Optional[str] = None

        # Page size
        limit: Optional[int] = None

        # Namespace filter
        namespace_name: Optional[str] = None

    
    @dataclass
    class ServicesRequestBuilderGetRequestConfiguration(RequestConfiguration[ServicesRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

