# midi-status-display
Use midi devise as a status display

Example usage is displaying usage bars (CPU or network usage, for example) and displaying if a service is responding with ping on a LCD matrix midi device, such as launchpads.

Configure applciation using `profile.json` and `config.json`.

CL applcaition arguments:
 * `--config` or `-c` - specify config path. Default is `config.json`
 * `--profile` or `-p` - specify prfile path Default is `profile.json`


Example of config
```json
{
  "DefaultDevice": "Launchpad Pro",
  "JellyfinUrl": "http://192.168.1.2:30013",
  "JellyfinApiKey": "xxxxxxxxxxxxxxxxxx",
  "TrueNasUrl": "http://192.168.1.2",
  "TrueNasApiKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
}
```

Example of profile
```json
{
  "Device": "Launchpad Pro", // auto connect device
  "Items": [
    {
      "TrackerTypes": "Ping",
      "UpdateInterval": 900000, // delay is ms. (default is 60000)
      "Items": [
        {
          "StatKey": "https://8.8.8.8",
          "Colors": [5, 27], // off on colors. (default is [0, 3])
          "PosX": 8,
          "PosY": 1,
          "Size": 0 // set to 0 for false as default
        },
        {
          "StatKey": "https://212.58.253.67",
          "Colors": [5, 27],
          "PosX": 8,
          "PosY": 2,
          "Size": 0
        }
      ]
    },
    {
      "TrackerTypes": "Jellyfin",
      "UpdateInterval": 60000,
      "Items": [
        {
          "StatKey": "active_users",
          "PosX": 1,
          "PosY": 1,
          "Size": 8 // size of bar
        }
      ]
    },
    {
      "TrackerTypes": "TrueNas",
      "UpdateInterval": 10000,
      "Items": [
        {
          "StatKey": "interface",
          "StatValue": "receive",
          "PosX": 2,
          "PosY": 1,
          "Size": 8,
          "Scale": 0.000125
        },
        {
          "StatKey": "interface",
          "StatValue": "sent",
          "PosX": 3,
          "PosY": 1,
          "Size": 8,
          "Scale": 0.000125
        }
      ]
    }
  ]
}
```