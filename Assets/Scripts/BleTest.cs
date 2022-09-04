﻿using System;
using System.Collections.Generic;
using System.Threading;
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


    //readonly string serviceUuid = "{15173000-4947-11e9-8646-d663bd873d93}"; // xsens dot battery service
    readonly string[] characteristicUuids = {
                                     "{15173001-4947-11e9-8646-d663bd873d93}",      // CUUID 1
                                     "{15173001-4947-11e9-8646-d663bd873d93}"       // CUUID 2
    };

    BLE ble;
    BLE.BLEScan scan;
    bool isScanning = false, isConnected = false;
    string deviceId = null;  
    IDictionary<string, string> discoveredDevices = new Dictionary<string, string>();
    int devicesCount = 0;

    // BLE Threads 
    Thread scanningThread, connectionThread, readingThread;

    // GUI elements
    public Text TextDiscoveredDevices, TextIsScanning, TextTargetDeviceConnection, TextTargetDeviceData;
    public Button ButtonEstablishConnection, ButtonStartScan;
    //int remoteAngle, lastRemoteAngle;
    private int batteryLevel, lastBatteryLevel, batteryStatus;
    

    // Start is called before the first frame update
    void Start()
    {
        ble = new BLE();
        ButtonEstablishConnection.enabled = false;
        TextTargetDeviceConnection.text = targetDeviceName + " not found.";
        readingThread = new Thread(ReadBleData);
    }

    // Update is called once per frame
    void Update()
    {  
        if (isScanning)
        {
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
            // Device was found, but not connected yet. 
            } else if (!ButtonEstablishConnection.enabled && !isConnected)
            {
                ButtonEstablishConnection.enabled = true;
                TextTargetDeviceConnection.text = "Found target device:\n" + targetDeviceName;
            } 
        } 
    }

    private void OnDestroy()
    {
        CleanUp();
    }

    public void OnApplicationQuit()
    {
        CleanUp();
    }

    // Prevent threading issues and free BLE stack.
    // Can cause Unity to freeze and lead
    // to errors when omitted.
    private void CleanUp()
    {
        try
        {
            scan.Cancel();
            ble.Close();
            scanningThread.Abort();
            connectionThread.Abort();
        } catch(NullReferenceException e)
        {
            Debug.Log("Thread or object never initialized.\n" + e);
        }        
    }

    public void StartScanHandler()
    {
        devicesCount = 0;
        isScanning = true;
        discoveredDevices.Clear();
        scanningThread = new Thread(ScanBleDevices);
        scanningThread.Start();
        TextIsScanning.color = new Color(244, 180, 26);
        TextIsScanning.text = "Scanning...";
        TextDiscoveredDevices.text = "";
    }

    public void ResetHandler()
    {
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
        byte[] packageReceived = BLE.ReadBytes();
        // Convert little Endian.
        // In this example we're interested about the battery
        // value on the first field of our package.
        //remoteAngle = packageReceived[0];
        batteryLevel = packageReceived[0];
        batteryStatus = packageReceived[1];

        Debug.Log(ReadBatteryDetails(batteryLevel, batteryStatus));
        //Thread.Sleep(100);


    }



    string ReadBatteryDetails(int inBatteryLevel, int inBatteryStatus)
    {
        string[] batteryStatusVerbose = { "[NOT CHARGING]", "[CHARGING]" };

        if (inBatteryStatus is 0 or 1)
        {
            return $"Battery Level: {inBatteryLevel}% {batteryStatusVerbose[inBatteryStatus]}";
        }
        else
        {
            return $"Battery Level: {inBatteryLevel}% and device charge status cannot be read.";
        }
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
                }
                break;
            case "connected":
                ButtonEstablishConnection.enabled = false;
                TextTargetDeviceConnection.text = "Connected to target device:\n" + targetDeviceName;
                break;
            case "writeData":
                if (!readingThread.IsAlive)
                {
                    readingThread = new Thread(ReadBleData);
                    readingThread.Start();
                }
                /*
                if (remoteAngle != lastRemoteAngle)
                {
                    TextTargetDeviceData.text = "Remote angle: " + remoteAngle;
                    lastRemoteAngle = remoteAngle;
                }
                */
                if (batteryLevel != lastBatteryLevel)
                {
                    //TextTargetDeviceData.text = "Battery Level: " + batteryLevel;
                    TextTargetDeviceData.text = ReadBatteryDetails(batteryLevel, batteryStatus);
                    lastBatteryLevel = batteryLevel;
                }
                break;
        }
    }

    void ScanBleDevices()
    {
        scan = BLE.ScanDevices();
        Debug.Log("BLE.ScanDevices() started.");
        scan.Found = (_deviceId, deviceName) =>
        {
            Debug.Log("found device with name: " + deviceName);
            discoveredDevices.Add(_deviceId, deviceName);

            if (deviceId == null && deviceName == targetDeviceName)
                deviceId = _deviceId;
        };

        scan.Finished = () =>
        {
            isScanning = false;
            Debug.Log("scan finished");
            if (deviceId == null)
                deviceId = "-1";
        };
        while (deviceId == null)
            Thread.Sleep(500);
        scan.Cancel();
        scanningThread = null;
        isScanning = false;

        if (deviceId == "-1")
        {
            Debug.Log("no device found!");
            return;
        }
    }

    // Start establish BLE connection with
    // target device in dedicated thread.
    public void StartConHandler()
    {
        connectionThread = new Thread(ConnectBleDevice);
        connectionThread.Start();
    }

    void ConnectBleDevice()
    {
        if (deviceId != null)
        {
            try
            {
                //ble.Connect(deviceId, serviceUuid, characteristicUuids);
                ble.Connect(deviceId, batteryServiceUuid, batteryCharacteristicsUuid);
            } 
            catch(Exception e)
            {
                Debug.Log("Could not establish connection to device with ID " + deviceId + "\n" + e);
            }
        }

        if (ble.isConnected)
        {
            Debug.Log("Connected to: " + targetDeviceName);
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
}
