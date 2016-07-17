﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

public class All : MonoBehaviour {

    public GameObject prefabDragItem;

    // global
    TcpListener listener;
    Recognition recognition;
    Adaption adaption;
    List<string> events;
    object eventsMutex;
    
    // ui component
    Text tSample;
    Text tinputted;
    Text[] tDragItems;
    string info0Text;

    // configuration
    string serverIP;
    public string algorithm;

    // sample and inputted
    List<string> inputtedWords;
    List<Vector2> inputtedPoints;
    List<Vector2[]> inputtedPointsAll;
    string[] sampleSentences;
    int sampleIndex;
    List<string> candidates;

    // typing
    int deviceWidth, deviceHeight;
    const int DRAG_ROW = 5;
    const int DRAG_COLUMN = 5;
    const int DRAG_ITEM_N = DRAG_ROW * DRAG_COLUMN;
    const float DRAG_SMOOTH = 1.0f;
    int dragStartX, dragStartY;
    int dragSpanX, dragSpanY;
    int selectX, selectY, selectIndex;

    // Record
    XFileWriter log;
    public XFileWriter deb;
    bool emptySentence;

    void Start()
    {
        log = new XFileWriter("log.txt");
        deb = new XFileWriter("deb.txt");

        SetupServer();
        recognition = new Recognition(this);
        adaption = new Adaption(this, recognition);
        events = new List<string>();
        eventsMutex = new object();
        
        tSample = GameObject.Find("Sample").GetComponent<Text>();
        tinputted = GameObject.Find("Inputted").GetComponent<Text>();
        tDragItems = new Text[DRAG_ITEM_N];
        GameObject canvas = GameObject.Find("Canvas");
        for (int i = 0; i < DRAG_ROW; i++)
            for (int j = 0; j < DRAG_COLUMN; j++)
            {
                GameObject gDragItem = Instantiate(prefabDragItem);
                gDragItem.transform.position = new Vector3(-150 + j * 105, -15 + i * -35, 700);
                gDragItem.transform.SetParent(canvas.transform);
                tDragItems[i * DRAG_COLUMN + j] = gDragItem.GetComponentInChildren<Text>();
            }
        recognition.ChangeMode();

        inputtedWords = new List<string>();
        inputtedPoints = new List<Vector2>();
        inputtedPointsAll = new List<Vector2[]>();
        sampleSentences = XFileReader.Read("phrases-normal.txt");
        for (int i = 0; i < sampleSentences.Length; i++) sampleSentences[i] = sampleSentences[i].ToLower();
        sampleIndex = -1;
        selectIndex = 0;
        
        UpdateSample();
        Updateinputted();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            //info0Text = Input.GetAxis("Mouse X") + " " + Input.GetAxis("Mouse Y");
            recognition.ChangeMode();
        }
        if (Input.GetKey(KeyCode.Escape))
        {
            log.Close();
            deb.Close();
            log = new XFileWriter("log.txt", false);
            deb = new XFileWriter("deb.txt", false);
        }
        GameObject.Find("Info0").GetComponent<Text>().text = info0Text;
        GameObject.Find("Info1").GetComponent<Text>().text = DateTime.Now.ToString("HH:mm:ss") + " <color=#d078d0>" + algorithm + "</color> <color=#00ee99>Now:" + sampleIndex + "</color>";
        lock (eventsMutex)
        {
            for (int i = 0; i < events.Count; i++)
            {
                string line = events[i];
                if (i < events.Count - 1 && line.Substring(0, 5) == "drag " && events[i + 1].Substring(0, 5) == "drag ") continue;
                string[] arr = line.Split(' ');
                switch (arr[0])
                {
                    case "devicesize":
                        deviceWidth = int.Parse(arr[1]);
                        deviceHeight = int.Parse(arr[2]);
                        break;
                    case "click":
                        Click(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "dragbegin":
                        DragBegin(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "drag":
                        Drag(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "dragend":
                        DragEnd(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "leftslip":
                        LeftSlip();
                        break;
                    case "rightslip":
                        RightSlip();
                        break;
                    case "downslip":
                        DownSlip();
                        break;
                    case "actiondown":
                        break;
                }
            }
            events.Clear();
        }
    }

    void Click(int x, int y)
    {
        if (emptySentence)
        {
            log.TimeWriteLine("sentence " + sampleSentences[sampleIndex]);
            emptySentence = false;
        }
        log.TimeWriteLine("click " + x + " " + y);
        inputtedPoints.Add(new Vector2(x, y));
        Updateinputted();
    }

    void LeftSlip()
    {
        log.TimeWriteLine("leftslip");
        if (inputtedPoints.Count == 0 && inputtedWords.Count != 0)
        {
            inputtedWords.RemoveAt(inputtedWords.Count - 1);
            inputtedPointsAll.RemoveAt(inputtedPointsAll.Count - 1);
        }
        if (inputtedPoints.Count > 0)
        {
            inputtedPoints.RemoveAt(inputtedPoints.Count - 1);
        }
        Updateinputted();
    }

    void RightSlip()
    {
        string[] sampleWords = sampleSentences[sampleIndex].Split(' ');
        if (inputtedPoints.Count == 0 && inputtedWords.Count == sampleWords.Length)
        {
            UpdateSample();
            Updateinputted();
        }
        else if (inputtedPoints.Count > 0)
        {
            log.TimeWriteLine("rightslip");
            Select(0);
        }
    }

    void DownSlip()
    {
        log.TimeWriteLine("downslip");
        inputtedPoints.Clear();
        Updateinputted();
    }

    void DragBegin(int x, int y)
    {
        if (inputtedPoints.Count == 0) return;
        log.TimeWriteLine("dragbegin");
        dragStartX = x;
        dragStartY = y;
        dragSpanX = Math.Min(Math.Max((deviceWidth - x - 40) / DRAG_COLUMN, 10), 80);
        dragSpanY = Math.Min(Math.Max((deviceHeight - y - 80) / DRAG_ROW, 10), 80);
    }
    void Drag(int x, int y)
    {
        if (inputtedPoints.Count == 0) return;
        float addition = DRAG_SMOOTH - 0.5f;
        float selectX2 = 1.0f * (x - dragStartX) / dragSpanX;
        float selectY2 = 1.0f * (y - dragStartY) / dragSpanY;
        selectX2 = Math.Min(Math.Max(selectX2, -addition), DRAG_COLUMN + addition);
        selectY2 = Math.Min(Math.Max(selectY2, -addition), DRAG_ROW + addition);
        if (Math.Abs(selectX2 - (selectX + 0.5)) > DRAG_SMOOTH)
        {
            selectX = (x - dragStartX) / dragSpanX;
            selectX = Math.Min(Math.Max(selectX, 0), DRAG_COLUMN - 1);
        }
        if (Math.Abs(selectY2 - (selectY + 0.5)) > DRAG_SMOOTH)
        {
            selectY = (y - dragStartY) / dragSpanY;
            selectY = Math.Min(Math.Max(selectY, 0), DRAG_ROW - 1);
        }
        selectIndex = selectY * DRAG_COLUMN + selectX;
        selectIndex = Math.Min(selectIndex, candidates.Count - 1);
        Updateinputted();
    }
    void DragEnd(int x, int y)
    {
        if (inputtedPoints.Count == 0) return;
        Drag(x, y);
        log.TimeWriteLine("dragend");
        Select(selectIndex);
        selectIndex = 0;
    }

    void Select(int index)
    {
        string[] sampleArray = sampleSentences[sampleIndex].Split(' ');
        string requireWord = (inputtedWords.Count < sampleArray.Length) ? sampleArray[inputtedWords.Count] : "";
        log.TimeWriteLine("select " + candidates[index] + " " + index + " " + candidates.Contains(requireWord));
        inputtedWords.Add(candidates[index]);
        inputtedPointsAll.Add(inputtedPoints.ToArray());
        inputtedPoints.Clear();
        Updateinputted();
    }

    void Updateinputted()
    {
        Color cIdle = new Color(0.5f, 0.9f, 1.0f);
        Color cSelected = new Color(1.0f, 0.8f, 0.0f);
        string inputtedTot = "";
        foreach (string word in inputtedWords) inputtedTot += word + " ";
        if (inputtedPoints.Count > 0)
        {
            candidates = recognition.Recognize(inputtedPoints);
            inputtedTot += "<color=#ff0000><b>" + candidates[0] + "</b></color>";
            for (int i = 0; i < DRAG_ITEM_N; i++)
            {
                tDragItems[i].text = candidates[i];
                tDragItems[i].transform.parent.GetComponent<Image>().color = (i == selectIndex) ? cSelected : cIdle;
            }
        }
        else
        {
            candidates = null;
            for (int i = 0; i < DRAG_ITEM_N; i++)
            {
                tDragItems[i].text = "";
                tDragItems[i].transform.parent.GetComponent<Image>().color = cIdle;
            }
        }
        inputtedTot += "<color=#ff5555>|</color>";
        tinputted.text = inputtedTot;
    }
    void UpdateSample()
    {
        adaption.AddData(inputtedPointsAll, inputtedWords);
        inputtedPointsAll.Clear();
        inputtedWords.Clear();
        sampleIndex = (sampleIndex + 1) % sampleSentences.Length;
        tSample.text = sampleSentences[sampleIndex];
        emptySentence = true;
    }

    void SetupServer()
    {
        int serverPort = 10309;
        string hostName = Dns.GetHostName();
        IPAddress[] addressList = Dns.GetHostAddresses(hostName);
        serverIP = null;
        foreach (IPAddress ip in addressList)
        {
            if (ip.ToString().IndexOf("192.168.") != -1)
            {
                serverIP = ip.ToString();
                break;
            }
            if (ip.ToString().Substring(0, 3) != "127" && ip.ToString().Split('.').Length == 4) serverIP = ip.ToString();
        }
        Debug.Log("setup:" + serverIP + "," + serverPort);
        info0Text = serverIP;
        listener = new TcpListener(IPAddress.Parse(serverIP), serverPort);
        Thread listenThread = new Thread(ListenThread);
        listenThread.Start();
    }
    void ListenThread()
    {
        listener.Start();
        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread receiveThread = new Thread(ReceiveThread);
            receiveThread.Start(client);
        }
    }
    void ReceiveThread(object clientObject)
    {
        TcpClient client = (TcpClient)clientObject;
        info0Text = "client in (" + client.Client.RemoteEndPoint.ToString() + ")";
        Debug.Log("client in");
        StreamReader reader = new StreamReader(client.GetStream());
        while (true)
        {
            string line;
            try
            {
                line = reader.ReadLine();
                if (line == null) break;
            }
            catch
            {
                break;
            }
            lock (eventsMutex)
            {
                events.Add(line);
            }
        }
        reader.Close();
        info0Text = serverIP;
        Debug.Log("client out");
    }
}

class XFileReader
{
    public static string[] Read(string filename)
    {
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            StreamReader reader = new StreamReader(new FileStream(Application.streamingAssetsPath + "/" + filename, FileMode.Open));
            List<string> lines = new List<string>();
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                lines.Add(line);
            }
            reader.Close();
            return lines.ToArray();
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            string url = Application.streamingAssetsPath + "/" + filename;
            WWW www = new WWW(url);
            while (!www.isDone) { }
            return www.text.Split('\n');
        }
        return new string[0];
    }
}

public class XFileWriter
{
    StreamWriter writer;
    string filename;

    public XFileWriter(string filename, bool append = true)
    {
        FileMode fileMode = append ? FileMode.Append : FileMode.Create;
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            writer = new StreamWriter(new FileStream(Application.dataPath + "//" + filename, fileMode));
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            writer = new StreamWriter(new FileStream(Application.persistentDataPath + "//" + filename, fileMode));
        }
    }

    public void TimeWriteLine(string s)
    {
        long nowTime = DateTime.Now.ToFileTimeUtc() / 10000 % 100000000;
        WriteLine(nowTime + " " + s);
    }

    public void WriteLine(string s)
    {
        writer.WriteLine(s);
        writer.Flush();
    }

    public void Close()
    {
        writer.Close();
    }

    ~XFileWriter()
    {
        Close();
    }
}