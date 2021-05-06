# FindDevice

FindDevice is a basic .NET command line tool you can use to look for devices on your local network or even link-local devices (such as devices that support USBNCM). It uses DNS Service Discovery (DNS-SD) to look for devices that advertise the given service. By default, it looks for devices running [Factory Orchestrator](https://github.com/microsoft/FactoryOrchestrator) (_factorch._tcp.local) but can be configured to look for any DNS-SD service, such as Windows Device Portal (_wdp.tcp.local).

FindDevice makes it easy to discover devices running a specified program/service, so that they can be then interacted with remotely.

## Usage
  FindDevice [options]

Options:
| Option |  Details | Default value |
| -- | -- | -- |
| --link-local-only | Only look for link-local devices, such as UsbNCM devices | false |
| --display-hostname | Display the device hostname | true |
| --display-ipv4 | Display the device IPv4 address(es) | true |
| --display-ipv6 | Display the device IPv6 address(es) | false |
| --timeout <timeout> | The amount of time in milliseconds to wait for responses (use greater than 2000ms for WiFi), after which the program exits. | Infinite |
| --query-interval <query-interval> | The amount of time in milliseconds to wait between queries | 1000ms |
| --service <service> | The DNS-SD service string used for discovery | _factorch._tcp.local |
  
## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
