using System;
using System.Collections.Generic;

namespace ParserLib
{
    public class Parser(byte SoM, byte EoM = 0x00)
    {
        #region Enums

        public enum ParserErrors
        {
            Checksum,
            MissingEom,
            FormatError,
            Size,
            MissingId,
            SyncError
        }

        public enum ParserStatus
        {
            Idle,
            WaitId,
            WaitData,
            WaitCheckSum,
            WaitEoM
        }

        public enum ParserResult { 
            Completed,
            Error,
            Parsing
        }


        #endregion

        #region Event handlers

        public class ParseCompletedEventArgs(byte id, List<byte> data) : EventArgs
        {
            public byte MessageId { get; } = id;
            public byte[] Data { get; } = [.. data];
        }

        public class ParseErrorEventArgs(ParserErrors Error, byte CheckSum = 0) : EventArgs
        {
            public byte CheckSum { get; } = CheckSum;
            public ParserErrors Error { get; } = Error;
        }

        public event EventHandler<ParseCompletedEventArgs>? OnParseCompleted;

        public event EventHandler<ParseErrorEventArgs>? OnParseError;

        #endregion

        public class Descriptor(byte Id, Descriptor.MessageType Type = Descriptor.MessageType.BoundedMessages, int Size = 0, bool CheckSum = false)
        {        
            public enum MessageType
            {
                BoundedMessages,
                SizedMessages
            }

            public byte Id { get; } = Id;
            public int Size { get; } = Size;
            public bool CheckSum { get; } = CheckSum;
            public MessageType Type { get; } = Type;

            public static Descriptor Create(byte Id, MessageType Type = MessageType.BoundedMessages, int Size = 0, bool CheckSum = false)
            {
                return new Descriptor(Id, Type, Size, CheckSum);
            }
        }

        public class DescriptorSizeNotSet : Exception
        {
            public DescriptorSizeNotSet()
            {
            }

            public DescriptorSizeNotSet(string message)
                : base(message)
            {
            }

            public DescriptorSizeNotSet(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public class DescriptorIdAliasing : Exception
        {
            public DescriptorIdAliasing()
            {
            }

            public DescriptorIdAliasing(string message)
                : base(message)
            {
            }

            public DescriptorIdAliasing(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        #region Private data

        private ParserStatus _Status;
        private readonly List<byte> _Data = [];
        private int _DataSize = 0;
        private Descriptor? _CurrentDescriptor = null;
        private byte _CheckSum = 0;

        #endregion

        #region Properties

        public byte SoM { get; } = SoM;
        public byte EoM { get; } = EoM;

        public ParserStatus Status { get => _Status; }

        public List<Descriptor> Descriptors { get; } = [];


        #endregion

        private Descriptor? GetDescriptor(byte id)
        {
            foreach (var descriptor in Descriptors)
                if (descriptor.Id.Equals(id))
                    return descriptor;

            return null;
        }

        public Parser AddDescriptor(Descriptor descriptor)
        {
            foreach (var descr in Descriptors)
                if (descr.Id.Equals(descriptor.Id))
                    throw new DescriptorIdAliasing("Descriptor Id aliasing");

            Descriptors.Add(descriptor);

            return this;
        }

        public Parser AddDescriptor(byte id)
        {
            Descriptors.Add(Descriptor.Create(id));

            return this;
        }

        public ParserResult Parse(byte data)
        {
            switch (Status)
            {
                case ParserStatus.Idle:
                    if (data.Equals(SoM))
                    {
                        _DataSize = 0;
                        _CheckSum = 0;

                        _Data.Clear();

                        _Status = ParserStatus.WaitId;
                    }
                    else
                    {
                        ParseError(ParserErrors.SyncError);
                        return ParserResult.Error;
                    }
                    break;

                case ParserStatus.WaitId:
                    _CurrentDescriptor = GetDescriptor(data);
                    if (_CurrentDescriptor != null)
                    {
                        _DataSize = _CurrentDescriptor.Size;
                        _Status = ParserStatus.WaitData;
                    }
                    else
                    {
                        _Status = ParserStatus.Idle;
                        ParseError(ParserErrors.MissingId);
                        return ParserResult.Error;
                    }
                    break;

                case ParserStatus.WaitData:
                    _Data.Add(data);

#pragma warning disable CS8602 // Dereferenziamento di un possibile riferimento Null.
                    if (_CurrentDescriptor.CheckSum)
                        _CheckSum += data;
#pragma warning restore CS8602 // Dereferenziamento di un possibile riferimento Null.

                    if (_CurrentDescriptor.Type == Descriptor.MessageType.SizedMessages)
                    {
                        _DataSize--;

                        if (_DataSize == 0)
                        {
                            _Status = ParserStatus.WaitEoM;

                            if (_CurrentDescriptor.CheckSum)
                                _Status = ParserStatus.WaitCheckSum;
                        }
                    }
                    else if (_CurrentDescriptor.Type == Descriptor.MessageType.BoundedMessages)
                    {
                        if (data.Equals(EoM))
                        {
                            _Status = ParserStatus.WaitEoM;

                            if (_CurrentDescriptor.CheckSum)
                                _Status = ParserStatus.WaitCheckSum;
                        }
                    }
                    break;

                case ParserStatus.WaitCheckSum:
                    if (data.Equals(_CheckSum))
                    {
                        if (_CurrentDescriptor.Type == Descriptor.MessageType.SizedMessages)
                        {
#pragma warning disable CS8602 // Dereferenziamento di un possibile riferimento Null.
                            ParseCompleted(_CurrentDescriptor.Id, _Data);
#pragma warning restore CS8602 // Dereferenziamento di un possibile riferimento Null.

                            _Status = ParserStatus.Idle;
                            return ParserResult.Completed;
                        }
                        else if (!_CurrentDescriptor.Type.Equals(Descriptor.MessageType.BoundedMessages))
                            _Status = ParserStatus.WaitEoM;
                    }
                    else
                    {
                        ParseError(ParserErrors.Checksum, data);

                        _Status = ParserStatus.Idle;

                        return ParserResult.Error;
                    }
                    break;

                case ParserStatus.WaitEoM:
                    _Status = ParserStatus.Idle;

                    if (data == EoM)
                    {
#pragma warning disable CS8602 // Dereferenziamento di un possibile riferimento Null.
                        ParseCompleted(_CurrentDescriptor.Id, _Data);
#pragma warning restore CS8602 // Dereferenziamento di un possibile riferimento Null.
                        return ParserResult.Completed;
                    }
                    else
                    {
                        ParseError(ParserErrors.MissingEom, data);
                        return ParserResult.Error;
                    }

                default:
                    ParseError(ParserErrors.FormatError, data);
                    _Status = ParserStatus.Idle;
                    return ParserResult.Error;
            }

            return ParserResult.Parsing;
        }

        public void ParseCompleted(byte id, List<byte> data)
        {
            OnParseCompleted?.Invoke(this, new ParseCompletedEventArgs(id, data));
        }

        public void ParseError(ParserErrors error, byte chksum = 0)
        {
            OnParseError?.Invoke(this, new ParseErrorEventArgs(error, chksum));
        }
    }
}
