using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace 網路暗棋
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        Bitmap[] chessbmp = new Bitmap[33];//0~32
        Bitmap[] blank = new Bitmap[2];//深色淺色的空白
        Bitmap board;//棋盤的底色
        Bitmap platebmp = new Bitmap(800, 400);//存棋盤800*400，每個棋子100*100
        Bitmap move_platebmp = new Bitmap(800, 400);//保存除了要移動棋子外的整個底板和目前棋子分佈的圖，移動棋子時可以一直重畫
        int offsetx, offsety,original_x,original_y;//拖曳棋子相對位置及來源棋子的位置
        Graphics g;//繪圖物件
        int[,] chesstable = new int[5, 9]; //記錄目前棋子狀況，蓋棋為負，正棋為正，0為空，棋盤左上角為1,1，紅棋1~16，藍棋17~32

        UdpClient u, s;//s傳送UDP物件，u接收UDP物件
        Thread th;  //listen監聽接收訊息執行緒
        string enemyIP = "";//對手IP位址
        string myIP = "";//自己的IP位址
        int enemy=1000;  //對手port
        string sendstr = "";  //傳送字串

        Boolean play = true;  //true代表可以下子,false代表不得下子
        int playchess = -1; //紅子 0 藍子 1 若還未確定（第一步）-1

        private void loadbitmap()    //載入相關圖檔
        {
            chessbmp[0] = new Bitmap(網路暗棋.Properties.Resources.背面);  //棋子背面
            chessbmp[1] = new Bitmap(網路暗棋.Properties.Resources.帥);    //紅棋1~16
            chessbmp[2] = new Bitmap(網路暗棋.Properties.Resources.仕); chessbmp[3] = new Bitmap(網路暗棋.Properties.Resources.仕);
            chessbmp[4] = new Bitmap(網路暗棋.Properties.Resources.相); chessbmp[5] = new Bitmap(網路暗棋.Properties.Resources.相);
            chessbmp[6] = new Bitmap(網路暗棋.Properties.Resources.車); chessbmp[7] = new Bitmap(網路暗棋.Properties.Resources.車);
            chessbmp[8] = new Bitmap(網路暗棋.Properties.Resources.傌); chessbmp[9] = new Bitmap(網路暗棋.Properties.Resources.傌);
            chessbmp[10] = new Bitmap(網路暗棋.Properties.Resources.砲); chessbmp[11] = new Bitmap(網路暗棋.Properties.Resources.砲);
            chessbmp[12] = new Bitmap(網路暗棋.Properties.Resources.兵); chessbmp[13] = new Bitmap(網路暗棋.Properties.Resources.兵);
            chessbmp[14] = new Bitmap(網路暗棋.Properties.Resources.兵); chessbmp[15] = new Bitmap(網路暗棋.Properties.Resources.兵);
            chessbmp[16] = new Bitmap(網路暗棋.Properties.Resources.兵);
            chessbmp[17] = new Bitmap(網路暗棋.Properties.Resources.將);   //藍棋17~32
            chessbmp[18] = new Bitmap(網路暗棋.Properties.Resources.士); chessbmp[19] = new Bitmap(網路暗棋.Properties.Resources.士);
            chessbmp[20] = new Bitmap(網路暗棋.Properties.Resources.象); chessbmp[21] = new Bitmap(網路暗棋.Properties.Resources.象);
            chessbmp[22] = new Bitmap(網路暗棋.Properties.Resources.車藍); chessbmp[23] = new Bitmap(網路暗棋.Properties.Resources.車藍);
            chessbmp[24] = new Bitmap(網路暗棋.Properties.Resources.馬); chessbmp[25] = new Bitmap(網路暗棋.Properties.Resources.馬);
            chessbmp[26] = new Bitmap(網路暗棋.Properties.Resources.包); chessbmp[27] = new Bitmap(網路暗棋.Properties.Resources.包);
            chessbmp[28] = new Bitmap(網路暗棋.Properties.Resources.卒); chessbmp[29] = new Bitmap(網路暗棋.Properties.Resources.卒);
            chessbmp[30] = new Bitmap(網路暗棋.Properties.Resources.卒); chessbmp[31] = new Bitmap(網路暗棋.Properties.Resources.卒);
            chessbmp[32] = new Bitmap(網路暗棋.Properties.Resources.卒);
            blank[0] = new Bitmap(網路暗棋.Properties.Resources.blank0); blank[1] = new Bitmap(網路暗棋.Properties.Resources.blank1);  //棋盤單格，0：淺色，1：深色
            board = new Bitmap(網路暗棋.Properties.Resources.底板);//整個棋盤800*400
        }
        private void resetboard()   //隨機重設chesstable的值，代表重排棋子，棋子值為負值
        {
            int[] temp = new int[33];
            int tmp;
            Random rnd = new Random();
            for (int i=1;i<5;i++)
                for (int j = 1; j < 9; j++)
                {
                    do
                        tmp = rnd.Next(1, 33);
                    while (temp[tmp] == 1);
                    temp[tmp] = 1;
                    chesstable[i, j] = -tmp;
                }
        }
        private void printboard()   //列印chesstable陣列值，除錯用
        {
            for (int i = 1; i < 5; i++)
            {
                for (int j = 1; j < 9; j++)
                    Console.Write("{0,4}", chesstable[i, j] + " ");
                Console.WriteLine();
            }
        }
        private void redraw()   //將platebmp所畫的動作貼到plate(picturebox)
        {
            if (plate.Image != null) plate.Image.Dispose();
            plate.Image = platebmp.Clone(new Rectangle(0, 0, platebmp.Width, platebmp.Height), platebmp.PixelFormat);
        }
        private string getmyIP()  //取得本機IPv4
        {
            string hn = Dns.GetHostName();
            IPAddress[] ip = Dns.GetHostEntry(hn).AddressList;
            for (int i = 0; i < ip.Length; i++)
                if (ip[i].AddressFamily == AddressFamily.InterNetwork)
                    return ip[i].ToString();
            return "";
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;//關閉跨執行緒的非法呼叫檢查機制，讓listen執行緒可以傳送資料給其他物件
            myIP = getmyIP();
            this.Text = myIP;   //標題顯示自己的IP
            resetboard();       //重設棋盤
            printboard();       //顯示chesstable值，Debug用
            loadbitmap();       //載入棋子圖案
            g = Graphics.FromImage(platebmp);//繪圖物件設定在platebmp，畫完本次的動作再將platebmp貼到plate picturebox上顯示出來
            g.DrawImage(board, new Rectangle(0, 0, 800, 400));//畫棋盤
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 8; j++)
                    g.DrawImage(chessbmp[0], new Rectangle(j * 100, i * 100, 100, 100));  //將畫面上整個棋盤都畫上棋子背面
            redraw();
        }

        private void button1_Click(object sender, EventArgs e)  //connect按鈕動作
        {
            enemyIP = EnemyPort.Text;             //輸入使用者打上去的對手IP位址 
            s = new UdpClient(enemyIP, enemy);   //建立s物件，對手IP及port = 10000
            th = new Thread(listen);             //建立監聽接收訊息執行緒
            th.Start();                          //執行緒開始執行
            button1.Enabled = false;
        }
        private void listen()                   //監聽執行緒
        {
            int self = 1000;                   //自己監聽的port，所以對手也要送到這個port
            int x, y,ox,oy;                    //對手移動棋子，x,y棋子的目標位置，ox,oy棋子的來源位置，chesstable陣列的註標位置，y代表列，x代表行
            u = new UdpClient(self);
            IPEndPoint EP = new IPEndPoint(IPAddress.Parse(myIP), self);
            while (true)
            {
                byte[] bu = u.Receive(ref EP);
                string[] cmd = Encoding.Default.GetString(bu).Split(',');
                switch (cmd[0])
                {
                    case "0":                               //接收對手按下replay後傳來字串格式：0,17,32......共32個隨機的棋子編號
                        for (int i = 0; i < 4; i++)
                            for (int j = 0; j < 8; j++)
                                g.DrawImage(chessbmp[0], new Rectangle(j * 100, i * 100, 100, 100));  // 重畫自己棋盤全蓋掉
                        redraw();
                        for (int i = 1; i < 33; i++)
                            chesstable[(i - 1) / 8 + 1, (i - 1) % 8 + 1] = int.Parse(cmd[i]);   //將接收到隨機棋子編號記錄在自己的chesstable陣列裡
                        play = true;                       //對手按replay，變成我先下
                        playchess = -1;                    //尚未翻棋，所以還不知道誰是紅棋
                        this.BackColor = Color.Yellow;     //底色設為Yellow，提示下子
                        saybox.Text = "我先下！！" + "\n\r";
                        break;
                    case "1":                              //接收對手移動棋子字串格式：1,4,1,3,1   將3,1棋子移到4,1
                        y = int.Parse(cmd[1]);x = int.Parse(cmd[2]);
                        oy = int.Parse(cmd[3]);ox = int.Parse(cmd[4]);
                        //Console.WriteLine(cmd[3] + ","+ cmd[4]);
                        g.DrawImage(chessbmp[chesstable[oy,ox]],new Rectangle((x-1)*100,(y-1)*100,100,100));//將來源位置棋子畫在目的位置
                        g.DrawImage(blank[(ox + oy) % 2], new Rectangle((ox - 1) * 100, (oy - 1) * 100, 100, 100));//來源位置填上淺或深色底板格子
                        redraw();
                        chesstable[y, x] = chesstable[oy, ox];
                        chesstable[oy, ox] = 0;
                        play = true;
                        this.BackColor = Color.Yellow;

                        break;
                    case "2":
                        y = int.Parse(cmd[1]); x = int.Parse(cmd[2]);// 接收對手翻棋字串格式：2,4,1，對手翻4,1位置的棋子
                        chesstable[y, x] = -chesstable[y, x];  //翻棋，chesstable的值由負值轉正值
                        g.DrawImage(chessbmp[chesstable[y, x]], new Rectangle((x - 1) * 100, (y - 1) * 100, 100, 100));  //畫上該位置的棋子
                        redraw();
                        play = true;
                        this.BackColor = Color.Yellow;
                        break;
                    case "3":                             //接收對手第一次翻棋字串格式：3,1,1,0，對手翻1,1位置的棋子，且對手翻到紅棋
                        y = int.Parse(cmd[1]);x = int.Parse(cmd[2]);
                        if (int.Parse(cmd[3])==0) { playchess = 1;this.Text = "藍棋"; } else { playchess = 0; this.Text = "紅棋"; }
                        chesstable[y, x] = -chesstable[y, x];
                        g.DrawImage(chessbmp[chesstable[y, x]], new Rectangle((x - 1) * 100, (y - 1) * 100, 100, 100));
                        redraw();
                        play = true;
                        this.BackColor = Color.Yellow;
                        break;
                     case "4":                          //接收對手在對話框所留言的內容
                        saybox.Text = "對手說：" + cmd[1] + "\r\n" + saybox.Text;
                        break;
                }
            }
        }
        private void send(string str)               //傳送字串
        {
            byte[] bs = Encoding.Default.GetBytes(str);
            s.Send(bs, bs.Length);
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)  //視窗關閉
        {
            if(th!=null) th.Abort();       //關閉執行緒
            if (u!=null) u.Close();        //UDP接收物件u關閉
            if (s != null) s.Close();      //UDP傳送物件s關閉
        }

        private void msgbox_KeyDown(object sender, KeyEventArgs e)  //對話框按Enter 則傳送對話訊息
        {
            if (e.KeyCode == Keys.Enter)
            {
                saybox.Text = "我說：" + msgbox.Text + "\r\n" + saybox.Text;
                send("4," + msgbox.Text);
                msgbox.Text = "";
            }
        }

        private void restart_Click(object sender, EventArgs e)   //按下replay重玩鍵，更正自己的棋盤，重畫蓋棋，並傳送字串格式：0,17,32......共32個隨機的棋子編號
        {
            resetboard();//更正自己的棋盤
            for(int i = 0; i < 4; i++)
                for (int j = 0; j < 8; j++)
                g.DrawImage(chessbmp[0], new Rectangle(j * 100, i * 100, 100, 100));//重畫蓋棋
            redraw();
            sendstr = "0";
            for (int i = 1; i < 5; i++)
                for (int j = 1; j < 9; j++)
                    sendstr += "," + chesstable[i, j];//傳送字串格式：0,17,32......共32個隨機的棋子編號
            send(sendstr);
           // Console.WriteLine(sendstr);
            play = false;
            playchess = -1;
            this.BackColor = Color.Ivory;
            saybox.Text = "";
        }

       

        private void plate_MouseMove(object sender, MouseEventArgs e) 
        {
            if (e.Button == MouseButtons.Left  && play )
            {
                if (chesstable[original_y / 100 + 1, original_x / 100 + 1]>0)
                {
                    g.DrawImage(move_platebmp, new Rectangle(0, 0, 800, 400)); //滑鼠移動時隨時重畫除了移動棋子以外的整個棋盤
                    g.DrawImage(chessbmp[chesstable[original_y / 100 + 1, original_x / 100 + 1]], new Rectangle(e.X - offsetx, e.Y - offsety, 100, 100));
                    //依照滑鼠現在的位置一直重畫移動的棋子圖
                    redraw();
                }
                
            }
        }
        private Boolean movable(int y,int x,int y1,int x1)//檢查是否是可以移動，false棋子畫回原位，true代表棋子可以移動
        {
            int self = chesstable[y1 / 100 + 1, x1 / 100 + 1];  //來源的棋子值
            int enemy = chesstable[y / 100 + 1, x / 100 + 1];   //目的的棋子值
            
            if (y >= 400 || x >= 800 || y < 0 || x < 0) return false;  //若移到不在棋盤的範圍，則移回去
            if (y == y1 && x == x1) return false;                      //若移動在一樣的位置，移回去
            if ((self - 1) / 16 != playchess) return false;            //若移動別人的棋子，移回去
            if ((self == 26 || self == 27 || self == 10 || self == 11) && enemy > 0 && Math.Abs(enemy / 17 - self / 17) == 1)  //移動是炮或包可以跳
            {
                int count = 0;
                if (x1 / 100 == x / 100) //同一行
                {
                    if (y / 100 > y1 / 100)
                    {//向下
                        for (int i = y1 / 100 + 1; i <= y / 100 + 1; i++)
                         if (chesstable[i, x / 100 + 1] != 0) count++; 
                    }
                    else
                    {//向上
                        for (int i = y1 / 100 + 1; i>= y / 100 + 1; i--)
                         if (chesstable[i, x / 100 + 1] != 0) count++; 
                    }
                    if (count == 3) return true;
                }
                if (y1 / 100 == y / 100)//同一列
                {
                    if (x / 100 > x1 / 100)
                    {//向右
                        for (int i = x1 / 100 + 1; i <= x / 100 + 1; i++)
                         if (chesstable[y / 100 + 1, i] != 0) count++; 
                    }
                    else
                    {//向左
                        for (int i = x1 / 100 + 1; i >= x / 100 + 1; i--)
                         if (chesstable[y / 100 + 1, i] != 0) count++; 
                    }
                    if (count == 3) return true;    //從來源位置到目的位置，在同一列或同一行，數到三個棋子（不為0）代表跳過一個棋子，所以可以跳
                }
            }
            if (Math.Abs(y1-y)==100 && Math.Abs(x1-x)==0  || Math.Abs(y1-y)==0 && Math.Abs(x1-x)==100)  //其他棋子上下移動一格才能動
            {
                if (enemy < 0) return false;   //移到蓋棋，移回去
                if (enemy == 0) return true;   //移到空格，可以
                if (enemy >0 && Math.Abs(self / 17 - enemy / 17) == 1)  //移到正面棋子，且紅到藍或藍到紅，可以，判斷一下
                {
                    if (enemy > 16) enemy -= 16;   //將藍棋-16
                    if (self > 16) self -= 16;     //將藍棋-16
                    if (self >= 12 && self <= 16 && enemy >= 12 && enemy <= 16) return true;  //卒可以吃兵，或相反
                    if (self >= 12 & self <= 16 && enemy == 1) return true;    //卒或兵可以吃帥或將
                    if (self == 1 && enemy >= 12 && enemy <= 16) return false;  //帥或將不可以吃卒或兵
                    if (self / 2 <= enemy / 2) return true;        //其他比較大可以吃比較小的
                }
            } else return false;                                           //其他都不能動，移回去
            return false;
        }
        private void plate_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && play )     //滑鼠確定且可以下子
            {
                int x = e.X / 100 * 100;      //螢幕棋子左上角位置
                int y = e.Y / 100 * 100;
                if (chesstable[original_y / 100 + 1, original_x / 100 + 1] > 0)//來源是正面棋，代表移動過來的位置
                {
                    if (movable(y, x, original_y, original_x))  //判斷是否可移動
                    {
                        g.DrawImage(move_platebmp, new Rectangle(0, 0, 800, 400));   //畫扣除移動棋子的部份棋盤
                        g.DrawImage(chessbmp[chesstable[original_y / 100 + 1, original_x / 100 + 1]], new Rectangle(x, y, 100, 100));//畫來源位置棋子到目的位置
                        redraw();
                        chesstable[y / 100 + 1, x / 100 + 1] = chesstable[original_y / 100 + 1, original_x / 100 + 1];//更改chesstable陣列值
                        chesstable[original_y / 100 + 1, original_x / 100 + 1] = 0;
                        send("1," + (y / 100 + 1) + ","+ (x / 100 + 1) + "," +( original_y / 100 + 1) + "," +  (original_x / 100 + 1));
                        //1,目標位址y,目標位址x,來源位址y,來源位址x
                        play = false;
                        this.BackColor = Color.Ivory;
                    }
                    else  //若是不可移動，重畫回去
                    {
                        g.DrawImage(move_platebmp, new Rectangle(0, 0, 800, 400));
                        g.DrawImage(chessbmp[chesstable[original_y / 100 + 1, original_x / 100 + 1]], new Rectangle(original_x, original_y, 100, 100));
                        redraw();
                    }
                }
                else if (chesstable[y / 100 + 1, x / 100 + 1] < 0)//按的是蓋棋，則畫翻過來的棋子
                {
                    chesstable[y / 100 + 1, x / 100 + 1] = -chesstable[y / 100 + 1, x / 100 + 1];
                    g.DrawImage(chessbmp[chesstable[y / 100 + 1, x / 100 + 1]], new Rectangle(x, y, 100, 100));
                    redraw();
                    if (playchess == -1)   //若是第一次翻棋，則記錄自己是紅棋還是藍棋，並傳送字串格式：3,1,1,0，自已翻1,1位置的棋子，且翻到紅棋
                    {
                        if (chesstable[y / 100 + 1, x / 100 + 1] >= 1 && chesstable[y / 100 + 1, x / 100 + 1] <= 16)
                        { playchess = 0; this.Text = "紅棋"; }//Red
                        else
                        { playchess = 1; this.Text = "藍棋"; }//Blue
                        send("3," + (y / 100 + 1) + "," + (x / 100 + 1)+"," + playchess);
                       
                    }
                    else               //若不是第一翻棋，則傳送字串格式：2,1,1，代表翻1,1位置的棋子
                    {
                        send("2," + (y / 100 + 1) + "," + (x / 100 + 1));
                    }
                    play = false;
                    this.BackColor = Color.Ivory;
                }
             }
        }

        private void plate_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left  && play )
            {
                int x = e.X / 100 * 100;//滑鼠按下左鍵時，記錄取到的螢幕棋子左上角位置
                int y = e.Y / 100 * 100;
                original_x = x;
                original_y = y;
                if (chesstable[y/100+1,x/100+1] > 0)//如果是正面棋子，代表要移動
                {
                    offsetx = e.X - x;  //滑鼠座標位置-螢幕棋子左上角位置，用來展現拖曳效果
                    offsety = e.Y - y;
                    g.DrawImage(blank[(x / 100 + y / 100) % 2], new Rectangle(x, y, 100, 100));//螢幕棋子位置畫上淺色或深色底板格子
                    move_platebmp = platebmp.Clone(new Rectangle(0, 0, 800, 400), platebmp.PixelFormat);//將選到棋子除掉的整個底板記錄在move_platebmp上
                    g.DrawImage(chessbmp[chesstable[y / 100 + 1, x / 100 + 1]], new Rectangle(x, y, 100, 100));
                    redraw();
                }
                
            }
        }
    }
}
