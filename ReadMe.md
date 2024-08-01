# Parser.NET

This's a simple protocol parser created mainly to deal with serial streams (like RS232, RS485..).

The usage is pretty simple: create a Parser class instance and add to it a message Descriptor
for any message you have to manage (use AddDescriptor() method and/or Descriptor.Create() factory).
Each Descriptor can be used to instruct Parser to operate on Bounded messages (i.e. with one start-of-message and 
one end-of-message markers) or Sized messages (i.e. with a start-of-message marker and a message lenth). 
If if you opt for Sized messages you need to specify the size for each message. Each message can optionally be 
checksum signed.

The parser base method is Parse() you should call and pass to it every byte of data received from the stream.

The *parsing* result is emitted trough OnParserCompleted event (and/or OnParserError).

Trough OnParserCompleted event handler the user receive the data associated with the received message (stripped 
of any overhead).