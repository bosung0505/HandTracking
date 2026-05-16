using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UDPReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 5052;

    [HideInInspector] public float[] data; // 선언만 해둠

    void Start()
    {
        //  [에러 방어 1] 인스펙터 창의 과거 기록을 무시하고 무조건 방 10개짜리 배열로 초기화!
        data = new float[10];

        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] dataByte = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(dataByte);

                string[] splitData = text.Split(',');

                //  [에러 방어 2] 받아온 데이터 개수와 배열 크기(10) 중 더 작은 값까지만 반복
                int length = Math.Min(splitData.Length, data.Length);

                for (int i = 0; i < length; i++)
                {
                    float.TryParse(splitData[i], out data[i]);
                }
            }
            catch (Exception e)
            {
                // 통신 에러가 나도 스레드가 죽지 않도록 조용히 넘김
            }
        }
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}