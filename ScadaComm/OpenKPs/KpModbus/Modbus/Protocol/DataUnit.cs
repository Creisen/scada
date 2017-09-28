﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scada.Comm.Devices.Modbus.Protocol
{
    /// <summary>
    /// Блок данных Modbus
    /// </summary>
    public abstract class DataUnit
    {
        /// <summary>
        /// Конструктор
        /// </summary>
        protected DataUnit()
            : this(TableTypes.DiscreteInputs)
        {
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        public DataUnit(TableTypes tableType)
        {
            Name = "";
            TableType = tableType;
            Address = 0;

            FuncCode = 0;
            ExcFuncCode = 0;
            ReqPDU = null;
            RespPduLen = 0;
            ReqADU = null;
            ReqStr = "";
            RespByteCnt = 0;
        }


        /// <summary>
        /// Получить или установить наименование
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Получить или установить тип таблицы даных
        /// </summary>
        public TableTypes TableType { get; set; }

        /// <summary>
        /// Получить или установить адрес начального элемента
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// Получить описание запроса для получения значений элементов
        /// </summary>
        public abstract string ReqDescr { get; }


        /// <summary>
        /// Код функции запроса
        /// </summary>
        public byte FuncCode { get; protected set; }

        /// <summary>
        /// Код функции, обозначающий исключение
        /// </summary>
        public byte ExcFuncCode { get; protected set; }

        /// <summary>
        /// Получить PDU запроса
        /// </summary>
        public byte[] ReqPDU { get; protected set; }

        /// <summary>
        /// Получить длину PDU ответа на запрос
        /// </summary>
        public int RespPduLen { get; protected set; }

        /// <summary>
        /// Получить ADU запроса
        /// </summary>
        public byte[] ReqADU { get; protected set; }

        /// <summary>
        /// Получить строку запроса в режиме ASCII
        /// </summary>
        public string ReqStr { get; protected set; }

        /// <summary>
        /// Получить длину ADU ответа на запрос
        /// </summary>
        public int RespAduLen { get; protected set; }

        /// <summary>
        /// Получить количество байт, которое указывается в ответе
        /// </summary>
        public byte RespByteCnt { get; protected set; }


        /// <summary>
        /// Инициализировать PDU запроса, рассчитать длину ответа
        /// </summary>
        public abstract void InitReqPDU();

        /// <summary>
        /// Инициализировать ADU запроса и рассчитать длину ответа
        /// </summary>
        public virtual void InitReqADU(byte devAddr, TransModes transMode)
        {
            if (ReqPDU != null)
            {
                int pduLen = ReqPDU.Length;

                switch (transMode)
                {
                    case TransModes.RTU:
                        ReqADU = new byte[pduLen + 3];
                        ReqADU[0] = devAddr;
                        ReqPDU.CopyTo(ReqADU, 1);
                        ushort crc = ModbusUtils.CalcCRC16(ReqADU, 0, pduLen + 1);
                        ReqADU[pduLen + 1] = (byte)(crc % 256);
                        ReqADU[pduLen + 2] = (byte)(crc / 256);
                        RespAduLen = RespPduLen + 3;
                        break;
                    case TransModes.ASCII:
                        byte[] aduBuf = new byte[pduLen + 2];
                        aduBuf[0] = devAddr;
                        ReqPDU.CopyTo(aduBuf, 1);
                        aduBuf[pduLen + 1] = ModbusUtils.CalcLRC(aduBuf, 0, pduLen + 1);

                        StringBuilder sbADU = new StringBuilder();
                        foreach (byte b in aduBuf)
                            sbADU.Append(b.ToString("X2"));

                        ReqADU = Encoding.Default.GetBytes(sbADU.ToString());
                        ReqStr = ModbusUtils.Colon + sbADU;
                        RespAduLen = RespPduLen + 2;
                        break;
                    default: // TransModes.TCP
                        ReqADU = new byte[pduLen + 7];
                        ReqADU[0] = 0;
                        ReqADU[1] = 0;
                        ReqADU[2] = 0;
                        ReqADU[3] = 0;
                        ReqADU[4] = (byte)((pduLen + 1) / 256);
                        ReqADU[5] = (byte)((pduLen + 1) % 256);
                        ReqADU[6] = devAddr;
                        ReqPDU.CopyTo(ReqADU, 7);
                        RespAduLen = RespPduLen + 7;
                        break;
                }
            }
        }

        /// <summary>
        /// Расшифровать PDU ответа
        /// </summary>
        public virtual bool DecodeRespPDU(byte[] buffer, int offset, int length, out string errMsg)
        {
            errMsg = "";
            bool result = false;
            byte respFuncCode = buffer[offset];

            if (respFuncCode == FuncCode)
            {
                if (length == RespPduLen)
                    result = true;
                else
                    errMsg = ModbusPhrases.IncorrectPduLength;
            }
            else if (respFuncCode == ExcFuncCode)
            {
                errMsg = length == 2 ? 
                    ModbusPhrases.DeviceError + ": " + ModbusUtils.GetExcDescr(buffer[offset + 1]) :
                    ModbusPhrases.IncorrectPduLength;
            }
            else
            {
                errMsg = ModbusPhrases.IncorrectPduFuncCode;
            }

            return result;
        }
    }
}