﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Numerics;
using Oracle.ManagedDataAccess.Client;

namespace PeopleCodeLib.Decoder
{
    class BooleanFrame
    {
        public int BoolIndent = 0;
        public BOOLEAN_TYPES lastSeenBool = BOOLEAN_TYPES.NONE;
        public bool isAndIndented = false;

        public BooleanFrame() { }

        public BooleanFrame(int indent)
        {
            BoolIndent = indent;
        }
    }
    enum BOOLEAN_TYPES
    {
        AND, OR, NONE
    };

    public class Parser
    {
        public static int parserRuns;
        public static Stopwatch timeParsing;
        public static long charsDecoded;

        static Parser()
        {
            timeParsing = new Stopwatch();
            parserRuns = 0;
        }

        int lastByte;
        int nextByte;

        bool lBracket;
        int nIndent;
        bool afterClassDefn = false;
        bool isAppClass = false;
        bool isInsideIf = false;
        bool isCompOrGblDefn = false;
        int unMatchedParens;
        MemoryStream ms;
        Stack<BooleanFrame> booleanFrames = new Stack<BooleanFrame>();

        BooleanFrame currentBoolFrame
        {
            get
            {
                return booleanFrames.Peek();
            }
        }

        StringBuilder OutputText = new StringBuilder();
        List<Dictionary<string, string>> References;
        private static string[] refKeywords = new string[] {"Component","Panel","RecName", "Scroll", "MenuName", "BarName", "ItemName", "CompIntfc",
                "Image", "Interlink", "StyleSheet", "FileLayout", "Page", "PanelGroup", "Message", "BusProcess", "BusEvent", "BusActivity",
                "Field", "Record","Operation","Portal","Node"};

        public Parser()
        {
            booleanFrames.Push(new BooleanFrame());
        }

        private void ResetBooleanFrame()
        {
            currentBoolFrame.isAndIndented = false;
            currentBoolFrame.BoolIndent = 0;
            currentBoolFrame.lastSeenBool = BOOLEAN_TYPES.NONE;
        }

        private void Write(string s)
        {
            OutputText.Append(s);
        }

        private string ReadPureString()
        {
            MemoryStream bytesRead = new MemoryStream();
            byte[] currentChar = new byte[2];

            ms.Read(currentChar, 0, 2);
            while (currentChar[0] + currentChar[1] > 0)
            {
                bytesRead.Write(currentChar, 0, 2);
                ms.Read(currentChar, 0, 2);
            }

            return Encoding.Unicode.GetString(bytesRead.ToArray());

        }
        private void WriteNewLineBefore()
        {
            if (OutputText.Length == 0)
            {
                return;
            }
            /*var lastChar = OutputText[OutputText.Length - 1];
            if (lastChar != '\n')
            {
                Write("\r\n");
            }*/
            if (OutputText.Length > 0 && OutputText[OutputText.Length - 1] != '\n')
            {
                bool onlyWhitespace = true;
                var x = OutputText.Length - 1;
                while (x > 0 && OutputText[x] != '\n')
                {
                    if (Char.IsWhiteSpace(OutputText[x]) == false)
                    {
                        onlyWhitespace = false;
                        break;
                    }
                    x--;
                }
                if (onlyWhitespace == false)
                {
                    Write("\r\n");
                }
            }
        }
        private string ReadNumber()
        {
            int numBytes = 18;

            int firstByte = ms.ReadByte();
            int decimalPlace = ms.ReadByte();

            BigInteger value = BigInteger.Zero;
            BigInteger fact = BigInteger.One;
            for (var i = 0; i < (numBytes - 4); i++)
            {
                value = BigInteger.Add(value, BigInteger.Multiply(fact, new BigInteger(ms.ReadByte())));
                fact = BigInteger.Multiply(fact, new BigInteger(256));
            }
            string number = value.ToString();

            if (decimalPlace > 0)
            {
                while (number.Length < decimalPlace)
                {
                    number = "0" + number;
                }
                number = number.Insert(number.Length - (decimalPlace), ".");
            }
            if (number.StartsWith("."))
            {
                number = "0" + number;
            }
            /* skip last 2 bytes */
            ms.ReadByte();
            ms.ReadByte();

            return number;
        }

        private string ReadReference()
        {


            var index1 = ms.ReadByte();
            var index2 = ms.ReadByte();

            var index = index2 * 256 + index1;

            var reference = References.Where(p => p["NAMENUM"] == (index + 1).ToString()).First();

            var RecordName = reference["RECNAME"].ToString();
            var ReferenceName = reference["REFNAME"].ToString();

            foreach (String keyword in refKeywords)
            {
                if (RecordName.Equals(keyword.ToUpper()))
                {
                    RecordName = keyword;
                    break;
                }
            }

            if (nextByte == 33)
            {
                return $"{RecordName}.{ReferenceName}";
            }
            else if (nextByte == 74)
            {
                return ReferenceName;
            }
            else if (nextByte == 72)
            {
                return $"{RecordName}.\"{ReferenceName.Trim()}\"";
            }
            else
            {
                return "";
            }

        }

        private string ReadComment()
        {
            const int WIDE_AND = 0xff;
            const int COMM_LEN_BYTE2_MULTIPLIER = 256;

            int commLen = ms.ReadByte() & WIDE_AND;
            commLen += (ms.ReadByte() & WIDE_AND) * COMM_LEN_BYTE2_MULTIPLIER;

            byte[] commentText = new byte[commLen];
            ms.Read(commentText, 0, commLen);

            //TODO: Validate this works as expected.
            return Encoding.Unicode.GetString(commentText);
        }

        private void WritePadding()
        {
            if (OutputText.Length == 0) { return; }
            if (OutputText[OutputText.Length - 1] == '\n')
            {
                for (var x = 0; x < nIndent + currentBoolFrame.BoolIndent; x++)
                {
                    Write("   ");
                }
            }
        }
        private void WriteNewLineBeforeBlockEnd()
        {
            while (OutputText.Length > 0 && OutputText[OutputText.Length - 1] == ' ')
            {
                OutputText.Length--;
            }
            if (OutputText.Length > 0 && OutputText[OutputText.Length - 1] != '\n')
            {
                Write("\r\n");
            }
        }
        private void WriteOperatorSpaceBefore()
        {
            if (lastByte == 20 || lastByte == 11 || lastByte == 76 || lastByte == 77 || lastByte == 78)
            {
                Write(" ");
            }
            else
            {
                WriteSpaceBefore();
            }
        }
        private void WriteSpaceBefore()
        {
            var shouldWriteSpace = true;
            if (OutputText.Length == 0)
            {
                return;
            }
            if (OutputText[OutputText.Length - 1] == ' ')
            {
                shouldWriteSpace = false;
            }

            if (OutputText[OutputText.Length - 1] == '\n')
            {
                shouldWriteSpace = false;
            }

            if (OutputText[OutputText.Length - 1] == '(' && nextByte != 29)
            {
                shouldWriteSpace = false;
            }

            if (lastByte == 5 || lastByte == 87 || lastByte == 70 || lastByte == 71 || lastByte == 78)
            {
                shouldWriteSpace = false;
            }
            if (lBracket)
            {
                lBracket = false;
                shouldWriteSpace = false;
            }
            if (shouldWriteSpace)
            {
                Write(" ");
            }
        }

        public static Tuple<string, string, bool> GetProgramByKeysCompare(OracleConnection conn, List<Tuple<int, string>> keys)
        {
            var decompiledText = GetProgramByKeys(conn, keys);
            var storedTextBuilder = new StringBuilder();

            using (var getProgText = new OracleCommand("SELECT PCTEXT FROM PSPCMTXT WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2 AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4 AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6 AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8 AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10 AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12 AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14 ORDER BY PROGSEQ", conn))
            {

                for (var x = 0; x < 7; x++)
                {
                    OracleParameter paramId = new OracleParameter();
                    paramId.OracleDbType = OracleDbType.Int32;
                    paramId.Value = keys[x].Item1;

                    OracleParameter paramValue = new OracleParameter();
                    paramValue.OracleDbType = OracleDbType.Varchar2;
                    paramValue.Value = keys[x].Item2;

                    getProgText.Parameters.Add(paramId);
                    getProgText.Parameters.Add(paramValue);
                }
                using (var reader = getProgText.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            storedTextBuilder.Append(reader.GetString(0));
                        }
                        catch (Exception e) {
                            Console.WriteLine("Error during decode: " + e.ToString());
                        }

                    }
                }
            }
            decompiledText = decompiledText.Replace("\r\n", "\n").Replace("\n", "\r\n");
            var storedText = storedTextBuilder.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n").Trim();

            return new Tuple<string, string, bool>(decompiledText, storedText, decompiledText.Equals(storedText));
        }

        public static string GetProgramByKeys(OracleConnection conn, List<Tuple<int, string>> keys)
        {
            while (keys.Count < 7)
            {
                keys.Add(new Tuple<int, string>(0, " "));
            }

            /* Prefer PSPCMTEXT over decoding */
            try
             {
                timeParsing.Start();
                 StringBuilder sb = new StringBuilder();
                 using (var getProgram = new OracleCommand("SELECT PCTEXT FROM PSPCMTXT WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2 AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4 AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6 AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8 AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10 AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12 AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14 ORDER BY PROGSEQ", conn))
                 {
                     for (var x = 0; x < 7; x++)
                     {
                         OracleParameter paramId = new OracleParameter();
                         paramId.OracleDbType = OracleDbType.Int32;
                         paramId.Value = keys[x].Item1;

                         OracleParameter paramValue = new OracleParameter();
                         paramValue.OracleDbType = OracleDbType.Varchar2;
                         paramValue.Value = keys[x].Item2;

                         getProgram.Parameters.Add(paramId);
                         getProgram.Parameters.Add(paramValue);
                     }

                     using (var reader = getProgram.ExecuteReader())
                     {
                         while (reader.Read())
                         {
                             var clob = reader.GetOracleClob(0);
                             sb.Append(clob.Value);
                             clob.Close();
                         }
                         reader.Close();
                     }
                 }
                 if (sb.Length > 0)
                 {
                    var retString = sb.ToString();
                    timeParsing.Stop();
                     return retString;
                 }
             } catch(Exception e)
             {
                timeParsing.Stop();
             }
            /* timeParsing.Start();*/
            MemoryStream ms = new MemoryStream();
            using (var getProgram = new OracleCommand("SELECT PROGTXT FROM PSPCMPROG WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2 AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4 AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6 AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8 AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10 AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12 AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14 ORDER BY PROGSEQ", conn))
            {

                for (var x = 0; x < 7; x++)
                {
                    OracleParameter paramId = new OracleParameter();
                    paramId.OracleDbType = OracleDbType.Int32;
                    paramId.Value = keys[x].Item1;

                    OracleParameter paramValue = new OracleParameter();
                    paramValue.OracleDbType = OracleDbType.Varchar2;
                    paramValue.Value = keys[x].Item2;

                    getProgram.Parameters.Add(paramId);
                    getProgram.Parameters.Add(paramValue);
                }
                using (var reader = getProgram.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        var blob = reader.GetOracleBlob(0);
                        byte[] data = new byte[blob.Length];
                        blob.Read(data, 0, (int)blob.Length);
                        ms.Write(data, 0, data.Length);
                        blob.Close();
                    }
                    reader.Close();
                }
                getProgram.Dispose();
            }
            List<Dictionary<string, string>> references = new List<Dictionary<string, string>>();

            //            using (var getReferences = new OracleCommand("SELECT NAMENUM, RECNAME, REFNAME, PACKAGEROOT, QUALIFYPATH FROM PSPCMNAME WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2 AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4 AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6 AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8 AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10 AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12 AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14 order by NAMENUM", conn))
            using (var getReferences = new OracleCommand("SELECT NAMENUM, RECNAME, REFNAME FROM PSPCMNAME WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2 AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4 AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6 AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8 AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10 AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12 AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14 order by NAMENUM", conn))
            {

                for (var x = 0; x < 7; x++)
                {
                    OracleParameter paramId = new OracleParameter();
                    OracleParameter paramValue = new OracleParameter();

                    paramId.OracleDbType = OracleDbType.Int32;
                    paramId.Value = keys[x].Item1;

                    paramValue.OracleDbType = OracleDbType.Varchar2;
                    paramValue.Value = keys[x].Item2;

                    getReferences.Parameters.Add(paramId);
                    getReferences.Parameters.Add(paramValue);
                }

                using (var reader = getReferences.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Dictionary<string, string> reference = new Dictionary<string, string>();
                        reference.Add("NAMENUM", reader.GetInt32(0).ToString());
                        reference.Add("RECNAME", reader.GetString(1));
                        reference.Add("REFNAME", reader.GetString(2));
                        //reference.Add("PACKAGEROOT", reader.GetString(3));
                        //reference.Add("QUALIFYPATH", reader.GetString(4));
                        references.Add(reference);
                    }
                    reader.Close();
                }

                getReferences.Dispose();
            }
            Parser p = new Parser();
            var returnText = p.ParsePPC(ms.ToArray(), references);
            parserRuns++;
            /* timeParsing.Stop(); */
            charsDecoded += returnText.Length;
            return returnText;
        }
        public string ParsePPC(byte[] ppcBytes, List<Dictionary<string, string>> refs)
        {
            References = refs;
            ms = new MemoryStream(ppcBytes);
            ms.Seek(37, SeekOrigin.Begin);
            nextByte = ms.ReadByte();
            while (nextByte != 7)
            {
                if (nextByte == 26 || nextByte == 63 || nextByte == 61 || nextByte == 62)
                {
                    nIndent--;
                }


                WritePadding();
                switch (nextByte)
                {
                    case 0:
                        /* Space Before */
                        WriteSpaceBefore();
                        /* Pure String */
                        Write(ReadPureString());
                        break;
                    case 1:
                        /* Space Before */
                        WriteSpaceBefore();
                        /* Pure String */
                        Write(ReadPureString());
                        break;
                    case 3:
                        while (OutputText.Length > 0 && Char.IsWhiteSpace(OutputText[OutputText.Length - 1]))
                        {
                            OutputText.Length--;
                        }
                        Write(",");
                        Write(" ");
                        break;
                    case 4:
                        WriteSpaceBefore();
                        Write("/");
                        Write(" ");
                        break;
                    case 5:
                        if (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        Write(".");
                        break;
                    case 6:
                        WriteOperatorSpaceBefore();
                        Write("=");
                        Write(" ");
                        break;
                    case 8:
                        WriteOperatorSpaceBefore();
                        Write(">=");
                        Write(" ");
                        break;
                    case 9:
                        WriteOperatorSpaceBefore();
                        Write(">");
                        Write(" ");
                        break;
                    case 10:
                        /* Space Before */
                        WriteSpaceBefore();
                        /* Pure String */
                        Write(ReadPureString());
                        break;
                    case 11:
                        Write("(");
                        unMatchedParens++;
                        booleanFrames.Push(new BooleanFrame(currentBoolFrame.BoolIndent));
                        break;
                    case 12:
                        WriteOperatorSpaceBefore();
                        Write("<=");
                        Write(" ");
                        break;
                    case 13:
                        WriteOperatorSpaceBefore();
                        Write("<");
                        Write(" ");
                        break;
                    case 14:
                        WriteOperatorSpaceBefore();
                        Write("-");
                        Write(" ");
                        break;
                    case 15:
                        WriteOperatorSpaceBefore();
                        Write("*");
                        Write(" ");
                        break;
                    case 16:
                        WriteOperatorSpaceBefore();
                        Write("<>");
                        Write(" ");
                        break;
                    case 18:
                        /* Space Before */
                        WriteSpaceBefore();
                        /* Pure String */
                        Write(ReadPureString());
                        break;
                    case 19:
                        WriteOperatorSpaceBefore();
                        Write("+");
                        Write(" ");
                        break;
                    case 20:
                        if (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        Write(")");
                        unMatchedParens--;
                        booleanFrames.Pop();
                        break;
                    case 21:
                        while (OutputText.Length > 0 && Char.IsWhiteSpace(OutputText[OutputText.Length - 1]))
                        {
                            if (OutputText[OutputText.Length - 1] == '\r')
                            {
                                OutputText.Length--;
                                break;
                            }
                            else
                            {
                                OutputText.Length--;
                            }
                        }

                        ResetBooleanFrame();

                        Write(";\r\n");
                        break;
                    case 22:
                        /* Space Before */
                        WriteSpaceBefore();
                        /* Pure String */
                        Write("\"");
                        Write(ReadPureString().Replace("\"", "\"\""));
                        Write("\"");
                        //Write(" ");
                        break;
                    case 24:
                        WriteOperatorSpaceBefore();
                        Write("And");
                        if (currentBoolFrame.isAndIndented == false)
                        {
                            currentBoolFrame.BoolIndent++;
                            currentBoolFrame.isAndIndented = true;
                        }
                        if (currentBoolFrame.lastSeenBool == BOOLEAN_TYPES.OR)
                        {
                            currentBoolFrame.BoolIndent++;
                        }
                        currentBoolFrame.lastSeenBool = BOOLEAN_TYPES.AND;
                        Write("\r\n");
                        break;
                    case 25:
                        while (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        if (OutputText[OutputText.Length - 1] != '\n')
                        {
                            Write("\r\n");
                        }
                        nIndent--;
                        WritePadding();
                        Write("Else");
                        Write("\r\n");
                        nIndent++;
                        break;
                    case 26:
                        /*if (Char.IsWhiteSpace(OutputText[OutputText.Length -1]) == false)
                        {
                            Write("\r\n");
                            WritePadding();
                        }*/
                        while (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        if (OutputText[OutputText.Length - 1] != '\n')
                        {
                            Write("\r\n");
                        }
                        WritePadding();
                        Write("End-If");
                        break;
                    case 27:
                        WriteSpaceBefore();
                        Write("Error");
                        Write(" ");
                        break;
                    case 28:
                        /* If */
                        WriteSpaceBefore();
                        Write("If");
                        isInsideIf = true;
                        Write(" ");
                        ResetBooleanFrame();
                        nIndent++;
                        break;
                    case 29:
                        WriteOperatorSpaceBefore();
                        Write("Not");
                        Write(" ");
                        break;
                    case 30:
                        WriteOperatorSpaceBefore();
                        Write("Or");
                        Write("\r\n");
                        if (currentBoolFrame.isAndIndented == false)
                        {
                            currentBoolFrame.BoolIndent++;
                            currentBoolFrame.isAndIndented = true;
                        }
                        if (currentBoolFrame.isAndIndented && currentBoolFrame.lastSeenBool == BOOLEAN_TYPES.AND && currentBoolFrame.BoolIndent > 1)
                        {
                            currentBoolFrame.BoolIndent--;
                        }
                        currentBoolFrame.lastSeenBool = BOOLEAN_TYPES.OR;
                        break;
                    case 31:
                        //WriteSpaceBefore();
                        if (OutputText[OutputText.Length - 1] != ' ')
                        {
                            Write(" ");
                        }
                        Write("Then");
                        isInsideIf = false;
                        currentBoolFrame.BoolIndent = 0;
                        Write("\r\n");
                        break;
                    case 32:
                        //TODO: Fix the "IfAA" that is found on the first hit of this
                        WriteSpaceBefore();
                        Write("Warning");
                        Write(" ");
                        break;
                    case 33:
                        if (lastByte != 11)
                        {
                            WriteSpaceBefore();
                        }
                        Write(ReadReference());
                        break;
                    case 35:
                        WriteOperatorSpaceBefore();
                        Write("|");
                        Write(" ");
                        break;
                    case 36:
                        WriteNewLineBefore();
                        WritePadding();
                        Write(ReadComment());
                        Write("\r\n");
                        break;
                    case 37:
                        if (OutputText.Length > 0 && Char.IsWhiteSpace(OutputText[OutputText.Length - 1]) == false)
                        {
                            Write("\r\n");
                            WritePadding();
                        }

                        Write("While");
                        Write(" ");
                        nIndent++;
                        break;
                    case 38:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("End-While");
                        break;
                    case 39:
                        if (OutputText.Length > 0 && Char.IsWhiteSpace(OutputText[OutputText.Length - 1]) == false)
                        {
                            Write("\r\n");
                            WritePadding();
                        }

                        Write("Repeat");
                        Write("\r\n");
                        nIndent++;
                        break;
                    case 40:
                        WriteNewLineBefore();
                        nIndent--;
                        while (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        WritePadding();
                        Write("Until");
                        Write(" ");
                        break;
                    case 41:
                        if (OutputText.Length > 0 && Char.IsWhiteSpace(OutputText[OutputText.Length - 1]) == false)
                        {
                            Write("\r\n");
                            WritePadding();
                        }

                        Write("For");
                        Write(" ");
                        nIndent++;
                        break;
                    case 42:
                        WriteSpaceBefore();
                        Write("To");
                        Write(" ");
                        break;
                    case 43:
                        WriteSpaceBefore();
                        Write("Step");
                        Write(" ");
                        break;
                    case 44:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("End-For");
                        break;
                    case 45:
                        while (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        var tempNext = ms.ReadByte();
                        ms.Seek(-1, SeekOrigin.Current);
                        if (isCompOrGblDefn && lastByte == 21 && tempNext == 21)
                        {
                            isCompOrGblDefn = false;
                            Write("\r\n");
                        }
                        else if (OutputText[OutputText.Length - 1] != '\n')
                        {
                            Write("\r\n");
                        }
                        break;
                    case 46:
                        //WriteSpaceBefore();
                        WriteNewLineBefore();
                        WritePadding();
                        Write("Break");
                        break;
                    case 47:
                        if (lastByte == 11)
                        {
                            Write(" ");
                        }
                        else
                        {
                            WriteSpaceBefore();
                        }
                        Write("True");
                        //Write(" ");
                        break;
                    case 48:
                        if (lastByte == 11)
                        {
                            Write(" ");
                        }
                        else
                        {
                            WriteSpaceBefore();
                        }
                        Write("False");
                        //Write(" ");
                        break;
                    case 49:
                        WriteNewLineBefore();
                        Write("Declare");
                        Write(" ");
                        break;
                    case 50:
                        if (lastByte != 49)
                        {
                            WriteNewLineBefore();
                        }
                        Write("Function");
                        Write(" ");

                        if (lastByte != 49)
                        {
                            nIndent = 0;
                            nIndent++;
                        }
                        break;
                    case 51:
                        WriteSpaceBefore();
                        Write("Library");
                        Write(" ");
                        break;
                    case 53:
                        WriteSpaceBefore();
                        Write("As");
                        Write(" ");
                        break;
                    case 54:
                        WriteSpaceBefore();
                        Write("Value");
                        Write(" ");
                        break;
                    case 55:
                        WriteNewLineBeforeBlockEnd();
                        nIndent = 0;
                        WritePadding();
                        Write("End-Function");
                        break;
                    case 56:
                        WriteSpaceBefore();
                        Write("Return");
                        Write(" ");
                        break;
                    case 57:
                        WriteSpaceBefore();
                        Write("Returns");
                        Write(" ");
                        break;
                    case 58:
                        WriteSpaceBefore();
                        Write("PeopleCode");
                        Write(" ");
                        break;
                    case 59:
                        WriteSpaceBefore();
                        Write("Ref");
                        Write(" ");
                        break;
                    case 60:
                        Write("Evaluate");
                        Write(" ");
                        nIndent++;
                        break;
                    case 61:
                        WriteNewLineBeforeBlockEnd();
                        WritePadding();
                        Write("When");
                        Write(" ");
                        nIndent++;
                        break;
                    case 62:
                        WriteNewLineBeforeBlockEnd();
                        WritePadding();
                        Write("When-Other");
                        Write("\r\n");
                        nIndent++;
                        break;
                    case 63:
                        WriteNewLineBeforeBlockEnd();
                        WritePadding();
                        Write("End-Evaluate");
                        break;
                    case 64:
                        /* Space Before */
                        WriteSpaceBefore();
                        /* Pure String */
                        Write(ReadPureString());
                        break;
                    case 65:
                        WriteSpaceBefore();
                        break;
                    case 66:
                        ResetBooleanFrame();
                        Write("");
                        break;
                    case 67:
                        WriteSpaceBefore();
                        Write("Exit");
                        Write(" ");
                        break;
                    case 68:
                        WriteNewLineBefore();
                        WritePadding();
                        Write("Local");
                        Write(" ");
                        break;
                    case 69:
                        if (OutputText.Length > 0 && OutputText[OutputText.Length - 1] != '\n')
                        {
                            Write("\r\n");
                            WritePadding();
                        }
                        Write("Global");
                        isCompOrGblDefn = true;
                        Write(" ");
                        break;
                    case 70:
                        Write("**");
                        break;
                    case 71:
                        WriteSpaceBefore();
                        Write("@");
                        break;
                    case 72:
                        WriteSpaceBefore();
                        Write(ReadReference());
                        Write(" ");
                        break;
                    case 73:
                        WriteSpaceBefore();
                        Write("set");
                        Write(" ");
                        if (afterClassDefn)
                        {
                            nIndent++;
                        }
                        break;
                    case 74:
                        WriteSpaceBefore();
                        Write(ReadReference());
                        Write(" ");
                        break;
                    case 75:
                        //WriteSpaceBefore();
                        if (OutputText[OutputText.Length - 1] != ' ')
                        {
                            Write(" ");
                        }
                        Write("Null");
                        Write(" ");
                        break;
                    case 76:
                        if (lastByte == 77 || lastByte == 20)
                        {
                            while (OutputText.Length > 0 && OutputText[OutputText.Length - 1] == ' ')
                            {
                                OutputText.Length--;
                            }
                        }
                        else
                        {
                            WriteSpaceBefore();
                        }
                        Write("[");
                        lBracket = true;
                        break;
                    case 77:
                        Write("]");
                        //Write(" ");
                        break;
                    case 78:
                        while (Char.IsWhiteSpace(OutputText[OutputText.Length - 1]))
                        {
                            OutputText.Length--;
                        }
                        if (lastByte == 78)
                        {
                            Write(" ");
                        }
                        else {
                            WriteSpaceBefore();
                        }
                        Write(ReadComment());
                        /*if (lastByte == 31 || lastByte == 21 || lastByte == 25)
                        {
                            Write("\r\n");
                        }*/
                        if (isInsideIf && (lastByte != 24 && lastByte != 30) || (isAppClass && afterClassDefn == false && unMatchedParens > 0))
                        {
                            /* do nothing */
                        }
                        else {
                            Write("\r\n");
                        }
                        break;
                    case 79:
                        Write("\r\n");
                        break;
                    case 80:
                        Write(ReadNumber());
                        break;
                    case 81:
                        WriteSpaceBefore();
                        Write("PanelGroup");
                        Write(" ");
                        break;
                    case 83:
                        WriteNewLineBefore();
                        Write("Doc");
                        Write(" ");
                        break;
                    case 84:
                        if (OutputText.Length > 0 && OutputText[OutputText.Length - 1] != '\n')
                        {
                            Write("\r\n");
                            WritePadding();
                        }
                        Write("Component");
                        Write(" ");
                        break;
                    case 85:
                        /*while (OutputText.Length > 0 && OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }*/
                        WriteSpaceBefore();
                        Write(ReadComment());
                        Write("\r\n");
                        break;
                    case 86:
                        WriteNewLineBefore();
                        Write("Constant");
                        Write(" ");
                        break;
                    case 87:
                        Write(":");
                        break;
                    case 88:
                        WriteSpaceBefore();
                        Write("import");
                        Write(" ");
                        break;
                    case 89:
                        WriteSpaceBefore();
                        Write("*");
                        Write(" ");
                        break;
                    case 90:
                        WriteNewLineBefore();
                        Write("class");
                        Write(" ");
                        nIndent = 0;
                        isAppClass = true;
                        nIndent++;
                        break;
                    case 91:
                        WriteNewLineBefore();
                        nIndent = 0;
                        while (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        nIndent = 0;
                        WritePadding();
                        Write("end-class");
                        afterClassDefn = true;
                        break;
                    case 92:
                        WriteSpaceBefore();
                        Write("extends");
                        Write(" ");
                        break;
                    case 93:
                        WriteSpaceBefore();
                        Write("out");
                        Write(" ");
                        break;
                    case 94:
                        //WriteSpaceBefore();
                        WriteNewLineBefore();
                        WritePadding();
                        Write("property");
                        Write(" ");
                        break;
                    case 95:
                        WriteSpaceBefore();
                        Write("get");
                        Write(" ");
                        if (afterClassDefn)
                        {
                            nIndent++;
                        }
                        break;
                    case 96:
                        WriteSpaceBefore();
                        Write("readonly");
                        Write(" ");
                        break;
                    case 97:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("private");
                        Write("\r\n");
                        nIndent++;
                        break;
                    case 98:
                        WriteSpaceBefore();
                        Write("instance");
                        Write(" ");
                        break;
                    case 99:
                        WriteNewLineBefore();
                        WritePadding();
                        Write("method");
                        Write(" ");
                        if (afterClassDefn)
                        {
                            nIndent++;
                        }
                        break;
                    case 100:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("end-method");
                        break;
                    case 101:
                        WriteSpaceBefore();
                        Write("try");
                        Write("\r\n");
                        nIndent++;
                        WritePadding();
                        break;
                    case 102:
                        while (OutputText[OutputText.Length - 1] == ' ')
                        {
                            OutputText.Length--;
                        }
                        nIndent--;
                        WritePadding();
                        Write("catch");
                        Write(" ");
                        nIndent++;
                        break;
                    case 103:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("end-try");
                        break;
                    case 104:
                        WriteSpaceBefore();
                        Write("throw");
                        Write(" ");
                        break;
                    case 105:
                        WriteSpaceBefore();
                        Write("create");
                        Write(" ");
                        break;
                    case 106:
                        if (afterClassDefn)
                        {
                            while (OutputText[OutputText.Length - 1] == ' ')
                            {
                                OutputText.Length--;
                            }
                            if (OutputText[OutputText.Length - 1] != '\n')
                            {
                                Write("\r\n");
                            }
                            nIndent--;
                            WritePadding();
                        }
                        else
                        {
                            WriteSpaceBefore();
                        }
                        Write("end-get");
                        Write(" ");
                        break;
                    case 107:
                        if (afterClassDefn)
                        {
                            while (OutputText[OutputText.Length - 1] == ' ')
                            {
                                OutputText.Length--;
                            }
                            if (OutputText[OutputText.Length - 1] != '\n')
                            {
                                Write("\r\n");
                            }
                            nIndent--;
                            WritePadding();
                        }
                        else
                        {
                            WriteSpaceBefore();
                        }
                        Write("end-set");
                        Write(" ");
                        break;
                    case 109:
                        /* Space Before */
                        WriteNewLineBefore();
                        /* Pure String */
                        Write("/+ ");
                        Write(ReadPureString());
                        Write(" +/");
                        Write("\r\n");
                        WritePadding();
                        break;
                    case 110:
                        WriteSpaceBefore();
                        Write("Continue");
                        Write(" ");
                        break;
                    case 111:
                        WriteSpaceBefore();
                        Write("abstract");
                        Write(" ");
                        break;
                    case 112:
                        WriteSpaceBefore();
                        Write("interface");
                        Write(" ");
                        nIndent++;
                        break;
                    case 113:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("end-interface");
                        Write("\r\n");
                        break;
                    case 114:
                        WriteSpaceBefore();
                        Write("implements");
                        Write(" ");
                        break;
                    case 115:
                        WriteNewLineBeforeBlockEnd();
                        nIndent--;
                        WritePadding();
                        Write("protected");
                        Write("\r\n");
                        nIndent++;
                        break;
                    case 117:
                        WriteSpaceBefore();

                        byte[] shortLen = new byte[2];
                        ms.Read(shortLen, 0, 2);
                        var stringLength = BitConverter.ToInt16(shortLen, 0);
                        byte[] stringData = new byte[stringLength];
                        ms.Read(stringData, 0, stringLength);
                        var str = Encoding.Unicode.GetString(stringData);

                        Write(str);


                        Write(" ");
                        break;
                    case 118:
                        WriteSpaceBefore();

                        shortLen = new byte[2];
                        ms.Read(shortLen, 0, 2);
                        stringLength = BitConverter.ToInt16(shortLen, 0);
                        stringData = new byte[stringLength];
                        ms.Read(stringData, 0, stringLength);
                        str = Encoding.Unicode.GetString(stringData);

                        Write(str);

                        Write("\r\n");
                        nIndent++;
                        break;
                    case 119:
                        nIndent--;
                        

                        shortLen = new byte[2];
                        ms.Read(shortLen, 0, 2);
                        stringLength = BitConverter.ToInt16(shortLen, 0);
                        stringData = new byte[stringLength];
                        ms.Read(stringData, 0, stringLength);
                        str = Encoding.Unicode.GetString(stringData).Replace("\n", "\r\n");

                        /* Special handling for #Else because it isn't a seperate "token" but lumped together with the commented out code */
                        if (str.StartsWith("#Else") && char.IsWhiteSpace(str[5]))
                        {
                            WriteNewLineBeforeBlockEnd();
                        }

                        /* Not sure by just in case the #If turns into one of these in the event that the "else" is the compiled side of the directive */
                        if(str.StartsWith("#If") && char.IsWhiteSpace(str[3]))
                        {
                            WriteNewLineBeforeBlockEnd();
                        }

                        WritePadding();
                        Write(str);
                        Write("\r\n");
                        nIndent++;
                        break;
                    case 120:
                        nIndent--;

                        shortLen = new byte[2];
                        ms.Read(shortLen, 0, 2);
                        stringLength = BitConverter.ToInt16(shortLen, 0);
                        stringData = new byte[stringLength];
                        ms.Read(stringData, 0, stringLength);
                        str = Encoding.Unicode.GetString(stringData).Replace("\n", "\r\n");

                        if (str == "#If" || str == "#Else" || str == "#End-If")
                        {
                            WriteNewLineBeforeBlockEnd();
                        }

                        WritePadding();
                        Write(str);
                        Write("\r\n");
                        WritePadding();
                        break;
                    case 121:
                        WriteSpaceBefore();
                        Write("ComponentLife");
                        Write(" ");
                        break;
                    default:
                        //Debugger.Break();
                        break;
                }

                lastByte = nextByte;
                nextByte = ms.ReadByte();

            }


            /*ProgramElement p = new ProgramElement();
            p.Parse(ms, state);
            

            return p;*/
            return OutputText.ToString().Trim();
        }
    }
}
