using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BleTest : MonoBehaviour
{
    /*
    // Change this to match your device.
    string targetDeviceName = "EnterDeviceNameHere";
    string serviceUuid = "{2a2b1072-5199-11eb-ae93-0242ac130002}";
    string[] characteristicUuids = {
         "{59c2f246-5199-11eb-ae93-0242ac130002}",      // CUUID 1
         "{617c753e-5199-11eb-ae93-0242ac130002}"       // CUUID 2
    };
    */

    private readonly string targetDeviceName = "Xsens DOT";
    private readonly string batteryServiceUuid = "{15173000-4947-11e9-8646-d663bd873d93}"; // xsens dot battery service
    private readonly string[] batteryCharacteristicsUuid = {"{15173001-4947-11e9-8646-d663bd873d93}"};
    private string uiConsoleMessages = "";

    BLE ble;
    BLE.BLEScan scan;
    bool isScanning = false, isConnected = false;
    string deviceId = null;  
    IDictionary<string, string> discoveredDevices = new Dictionary<string, string>();
    int devicesCount = 0;

    // BLE Threads 
    Thread scanningThread, connectionThread, readingThread, streamingThread, testThread;

    // GUI elements
    public Text TextDiscoveredDevices, TextIsScanning, TextTargetDeviceConnection, TextTargetDeviceData;
    public TextMeshProUGUI UiConsoleText, UiStreamingText;
    public Button ButtonEstablishConnection, ButtonStartScan, ButtonStartStreaming, ButtonStopStreaming, BlinkButton;

    private int batteryLevel, lastBatteryLevel, batteryStatus;
    

    // Start is called before the first frame update
    void Start()
    {
        //UiConsoleText = GetComponent<TextMeshProUGUI>();
        ble = new BLE();
        ButtonEstablishConnection.enabled = false;
        //ButtonStopStreaming.enabled = false;
        //ButtonStartStreaming.enabled = false;
        //BlinkButton.enabled = false;
        TextTargetDeviceConnection.text = targetDeviceName + " not found.";
        readingThread = new Thread(ReadBleData);
        PrintToUiConsole("Console Ready!");
        UiStreamingText.text = "Streaming output text area is linked and ready!";
    }

    // Update is called once per frame
    void Update()
    {  
        if (isScanning)
        {
            // if you are currently scanning, you can't hit scan
            if (ButtonStartScan.enabled)
            {
                ButtonStartScan.enabled = false;
            }

            if (discoveredDevices.Count > devicesCount)
            {
                UpdateGuiText("scan");
                devicesCount = discoveredDevices.Count;
            }                
        }
        else
        {
            /* Restart scan in same play session not supported yet.
            if (!ButtonStartScan.enabled)
                ButtonStartScan.enabled = true;
            */
            if (TextIsScanning.text != "Not scanning.")
            {
                TextIsScanning.color = Color.white;
                TextIsScanning.text = "Not scanning.";
            }
        }

        // The target device was found.
        if (deviceId != null && deviceId != "-1")
        {
            // Target device is connected and GUI knows.
            if (ble.isConnected && isConnected)
            {
                UpdateGuiText("writeData");
            }
            // Target device is connected, but GUI hasn't updated yet.
            else if (ble.isConnected && !isConnected)
            {
                UpdateGuiText("connected");
                isConnected = true;
            } 
            // Device was found, but not connected yet. 
            else if (!ButtonEstablishConnection.enabled && !isConnected)
            {
                ButtonEstablishConnection.enabled = true;
                TextTargetDeviceConnection.text = "Found target device: " + targetDeviceName;
            } 
        }

        if (ble!= null)
        {
            if (ble.isConnected)
            {
                //Debug.Log("Connected to: " + targetDeviceName);
                UiStreamingText.text = $"BLE connection to {targetDeviceName} successful";
            }
            else
            {
                //Debug.Log("No Connection to: " + targetDeviceName);
                UiStreamingText.text = $"BLE connection to {targetDeviceName} unsuccessful";

            }
        }
    }

    private void OnDestroy()
    {
        PrintToUiConsole("OnDestroy has been called...");
        CleanUp();
    }

    public void OnApplicationQuit()
    {
        PrintToUiConsole("Closing Application...");
        CleanUp();
    }

    // Prevent threading issues and free BLE stack.
    // Can cause Unity to freeze and lead to errors when omitted.
    private void CleanUp()
    {
        try
        {
            scan.Cancel();
            ble.Close();
            scanningThread.Abort();
            connectionThread.Abort();
        }
        catch(NullReferenceException e)
        {
            Debug.Log("Thread or object never initialized.\n" + e);
        }        
    }

    // Hit the scan button
    public void StartScanHandler()
    {
        PrintToUiConsole("Scan started");
        devicesCount = 0;
        isScanning = true;
        discoveredDevices.Clear();
        scanningThread = new Thread(ScanBleDevices);
        scanningThread.Start();
        //TextIsScanning.color = new Color(244, 180, 26);
        TextIsScanning.text = "Scanning...";
        TextDiscoveredDevices.text = "";
    }

    // Hit the restart button
    public void ResetHandler()
    {
        PrintToUiConsole("Restarting");
        TextTargetDeviceData.text = "";
        TextTargetDeviceConnection.text = targetDeviceName + " not found.";
        // Reset previous discovered devices
        discoveredDevices.Clear();
        TextDiscoveredDevices.text = "No devices.";
        deviceId = null;
        CleanUp();
    }

    private void ReadBleData(object obj)
    {
        byte[] packageReceived = ble.ReadBytes();

        Debug.Log($">>>> ReadBleData {packageReceived.Length}");


        batteryLevel = packageReceived[0];
        batteryStatus = packageReceived[1];

        Debug.Log(ReadBatteryDetails(batteryLevel, batteryStatus));
        //Thread.Sleep(100);
    }

    
    string ReadBatteryDetails(int inBatteryLevel, int inBatteryStatus)
    {
        string[] batteryStatusVerbose = { "[NOT CHARGING]", "[CHARGING]" };
        string outString = "";

        if (inBatteryStatus is 0 or 1)
        {
            outString = $"Battery Level: {inBatteryLevel}% {batteryStatusVerbose[inBatteryStatus]}";
        }
        else
        {
            outString = $"Battery Level: {inBatteryLevel}% and device charge status cannot be read.";
        }
        //PrintToUiConsole(outString);
        Debug.Log(outString);
        return outString;
    }

    void UpdateGuiText(string action)
    {
        switch(action)
        {
            case "scan":
                TextDiscoveredDevices.text = "";
                foreach (KeyValuePair<string, string> entry in discoveredDevices)
                {
                    TextDiscoveredDevices.text += "DeviceID: " + entry.Key + "\nDeviceName: " + entry.Value + "\n\n";
                    Debug.Log("Added device: " + entry.Key);
                    //PrintToUiConsole("Added device: " + entry.Key);
                }
                break;

            case "connected":
                ButtonEstablishConnection.enabled = false;
                TextTargetDeviceConnection.text = "Connected to: " + targetDeviceName;
                break;

            case "writeData":
                if (!readingThread.IsAlive)
                {
                    readingThread = new Thread(ReadBleData);
                    readingThread.Start();
                }

                if (batteryLevel != lastBatteryLevel)
                {
                    //TextTargetDeviceData.text = "Battery Level: " + batteryLevel;
                    TextTargetDeviceData.text = ReadBatteryDetails(batteryLevel, batteryStatus);
                    lastBatteryLevel = batteryLevel;
                }
                break;
        }
    }


    void readtest()
    {
        string batteryServiceUuid = "{15173000-4947-11e9-8646-d663bd873d93}";
        string[] batteryCharacteristicsUuid = {"{15173001-4947-11e9-8646-d663bd873d93}"};
        

        

        // Read all bytes :
        byte[] packageReceived = ble.ReadBytes();

        Debug.Log($">>>> ReadBleData {packageReceived.Length}");


        batteryLevel = packageReceived[0];
        batteryStatus = packageReceived[1];

        Debug.Log(ReadBatteryDetails(batteryLevel, batteryStatus));

    }



    void ScanBleDevices() // runs in the scanning thread
    {
        scan = BLE.ScanDevices();
        Debug.Log("BLE.ScanDevices() started.");
        //PrintToUiConsole("BLE.ScanDevices() started");
        scan.Found = (_deviceId, deviceName) =>
        {
            Debug.Log($"Found device with name: {deviceName} and ID: {_deviceId}");
            discoveredDevices.Add(_deviceId, deviceName);

            // finding the Xsens DOT
            if (deviceId == null && deviceName == targetDeviceName)
            {
                deviceId = _deviceId;
            }
        };

        scan.Finished = () =>
        {
            isScanning = false;
            Debug.Log("Scanning finito");
            //PrintToUiConsole("scan finished");
            if (deviceId == null)
            {
                deviceId = "-1";
            }
        };

        while (deviceId == null)
        {
            Thread.Sleep(500);
        }

        scan.Cancel();
        scanningThread = null;
        isScanning = false;

        if (deviceId == "-1")
        {
            Debug.Log("no device found!");
            //PrintToUiConsole("no device found!");
            return;
        }
    }

    // Start establish BLE connection with target device in dedicated thread.
    public void StartConHandler()
    {
        PrintToUiConsole("Connection Attempt");
        connectionThread = new Thread(ConnectBleDevice);
        connectionThread.Start();
    }

    // Establish BLE connection and Stream.
    public void StartStreamHandler()
    {
        PrintToUiConsole("Starting Streaming");
        streamingThread = new Thread(StreamData);
        streamingThread.Start();
    }

    public void BlinkHandle()
    {
        /*
        PrintToUiConsole("Send blink handle");
        testThread = new Thread(TryBlinking);
        testThread.Start();
        */

        PrintToUiConsole("Running read test");
        testThread = new Thread(readtest);
        testThread.Start();
    }

    void TryBlinking()
    {
        if (isConnected)
        {
            if (ble != null)
            {
                //byte[] dataStruct = new byte[1];
                //dataStruct[1] = 0x01;

                ble.XsensBlink(deviceId, "15171000-4947-11e9-8646-d663bd873d93", "15171002-4947-11e9-8646-d663bd873d93", new byte[] { 0x01 });
            }
        }
        else
        {
            PrintToUiConsole($"Not connected to {targetDeviceName} and hence cannot blink my dude");
        }
    }

    public void StopStreamHandler()
    {
        PrintToUiConsole("Stopping Streaming");
        if (streamingThread != null)
        {
            if (streamingThread.IsAlive)
            {
                streamingThread.Abort();
            }
        }
    }

    void StreamData()
    {
        if (deviceId != null)
        {

        }
    }

    void ConnectBleDevice()
    {
        if (deviceId != null)
        {
            try
            {
                ble.Connect(deviceId, batteryServiceUuid, batteryCharacteristicsUuid);
            }
            catch(Exception e)
            {
                Debug.Log("Could not establish connection to device with ID " + deviceId + "\n" + e);
                //PrintToUiConsole("Could not establish connection to device with ID " + deviceId + "\n" + e);
            }
        }

        if (ble.isConnected)
        {
            Debug.Log("Connected to: " + targetDeviceName);
            //PrintToUiConsole("Connected to: " + targetDeviceName);
        }
    }

    ulong ConvertLittleEndian(byte[] array)
    {
        int pos = 0;
        ulong result = 0;
        foreach (byte by in array)
        {
            result |= ((ulong)by) << pos;
            pos += 8;
        }
        return result;
    }

    void PrintToUiConsole(string newEvent)
    {
        uiConsoleMessages = $"[{DateTime.Now.ToLongTimeString()}] {newEvent}\n{uiConsoleMessages}";
        //uiConsoleMessages = msg + "\n" + uiConsoleMessages;
        UiConsoleText.text = uiConsoleMessages;
        Debug.Log(newEvent);
    }


}
