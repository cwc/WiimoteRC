using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Resources;

/*
 * WiimoteLib is available from http://www.codeplex.com/WiimoteLib
 *
 * The version used here is probably 1.2 (from October 2007). Newer versions
 * may or may not work with this code.
 */
using WiimoteLib;

namespace WiimoteRC {
    class WiimoteRC {
		static bool bRunning;

        private Wiimote wiimote;
        private SerialPort rcPort;
        private ResourceManager rm;

        private bool m_bConnected;

        private int x;
        private int y;
        private int z;

        public WiimoteRC() {
            wiimote = new Wiimote();
            rcPort = new SerialPort();

			// ResourceManager is used to get string values used to communicate with the bot from the external resources file
            rm = new ResourceManager("WiimoteRC.WiimoteRC-Resources", System.Reflection.Assembly.GetExecutingAssembly());

            Debug.Listeners.Add(new TextWriterTraceListener(System.Console.Out));

            m_bConnected = false;

            // TODO: Provide ways to configure serial port data
            rcPort.BaudRate = 2400;
            rcPort.DataBits = 8;
            rcPort.Parity = Parity.None;
            rcPort.StopBits = StopBits.One;

            rcPort.ReadTimeout = 1500;
            rcPort.WriteTimeout = 1500;

            wiimote.WiimoteChanged += new WiimoteChangedEventHandler(wiimote_WiimoteChanged);
        }
		
		/*
		 * This event handler is called any time the wiimote's state changes. This
		 * includes acceleration data (waggle).
		 */
        void wiimote_WiimoteChanged(object sender, WiimoteChangedEventArgs args) {
            x = (int)(args.WiimoteState.AccelState.X * 100) + 100;
            y = (int)(args.WiimoteState.AccelState.Y * 100) + 100;
            z = (int)(args.WiimoteState.AccelState.Z * 100) + 100;

			if (x <= 0) {
				x = 1;
			}

			if (y <= 0) {
				y = 1;
			}

			if (z <= 0) {
				z = 1;
			}
			
			if (x > 90 && x < 110) {
                x = 0;
            }

            if (y > 90 && y < 110) {
                y = 0;
            }

            if (z > 90 && z < 110) {
                z = 0;
            }

			// NOTE: Disabled to keep from accidentally dis-/reconnecting while testing
			//if (args.WiimoteState.ButtonState.A && m_bConnected) {
			//    reconnect();
			//}

			//if (args.WiimoteState.ButtonState.B && m_bConnected) {
			//    disconnect();

			//    bRunning = false;
			//}
        }

        public int getX() {
            return x;
        }

        public int getY() {
            return y;
        }

        public int getZ() {
            return z;
        }

        public void reconnect() {
            disconnect();
            connect();
        }

        public void disconnect() {
            m_bConnected = false;

            rcPort.DiscardInBuffer();
            rcPort.DiscardOutBuffer();

            send(rm.GetString("RST"));

            lock (rcPort) {
                rcPort.Close();
            }

            wiimote.Disconnect();
        }

		/*
		 * This method loops through all detected serial ports and attempts to handshake
		 * with the bot.
		 *
		 * A reset (RST) string is sent first, in case the bot is not already waiting on
		 * a SYN string. The SYN is sent right afterwards, and a line is read from the
		 * port. If an ACK string is received, we have a successful connection.
		 *
		 * All of these strings are defined in the resources file.
		 */
        private bool connectSerialPort() {
            bool bConnected = false;

            string[] portNames = SerialPort.GetPortNames();

            for (int i = 0; !bConnected && i < portNames.Length; i++) {
                lock (rcPort) {
                    if (rcPort.IsOpen) {
                        rcPort.Close();
                    }
                }

                rcPort.PortName = portNames[i];

                try {
                    Debug.WriteLine("Trying " + rcPort.PortName);
                    rcPort.Open();

                    send(rm.GetString("RST"));
					rcPort.DiscardOutBuffer();

                    send(rm.GetString("SYN"));
                    Debug.WriteLine(rm.GetString("SYN") + " sent");

                    rcPort.DiscardInBuffer();

                    string s = rcPort.ReadLine();

                    if (s.Equals(rm.GetString("ACK"))) {
                        Debug.WriteLine(s + " received");

                        bConnected = true;
                    } else {
                        Debug.WriteLine("Unknown response: " + s);
                    }
                } catch (Exception e) {
                    Debug.WriteLine(e.Message);
                }

                if (!bConnected) {
					lock (rcPort) {
						rcPort.Close();
					}
                }
            }

            return bConnected;
        }

        private bool connectWiimote() {
            bool bConnected = false;

            try {
                wiimote.Connect();

                wiimote.SetReportType(Wiimote.InputReport.IRAccel, true);

                wiimote.SetLEDs(false, true, true, false);

                bConnected = true;
            } catch (Exception e) {
                if (false) {
                } else {
                    Debug.WriteLine(e);
                }
            }

            return bConnected;
        }

        public bool connect() {
            m_bConnected = connectWiimote() && connectSerialPort();

            if (m_bConnected) {
                Debug.WriteLine("\nConnected!\n\n");
            } else {
                Debug.WriteLine("\nUnable to connect.\n\n");
            }

            return m_bConnected;
        }

        public bool isConnected() {
            return m_bConnected;
        }

        public void send(string text) {
            try {
                rcPort.Write(text);
            } catch {
            }
        }

		/*
		 * The main program loop sends data in the form "xNNNyNNN". This is parsed
		 * by the programming on the bot and used to decide how to rotate the wheels.
		 */
        public static void Main(string[] args) {
            bRunning = true;

            WiimoteRC rc = new WiimoteRC();

            while (bRunning) {
                if (rc.isConnected()) {
					string cmd = "x" + rc.getX() + "y" + rc.getY() + "\n";

                    rc.send(cmd);

					Console.CursorTop = Console.CursorTop - 1;
					Console.Write("                               ");
					Console.CursorLeft = 0;

					Debug.Write(cmd);
                } else {
                    rc.connect();
                }
            }
        }
    }
}
