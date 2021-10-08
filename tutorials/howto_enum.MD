In my last project I wanted some multiple-choice-proberties (enumerations).
This is how this can be done:

```C#
private enum pEncodingType { BINARY, ASCII, UTF8, UTF16LE, UTF16BE, UTF32, Systemdefault }
```
First define a C# enum

```C#
new PropertyDesc("Encoding", new Enumeration(typeof(pEncodingType),false), 104, "How to encode text data.", "Connection")
```
In the **PropertyDesc** definition we create a new Enumeration object with the type of our enum 

```C#
private long encoding;
...or...
private pEncodingType encoding;
```
The property comes from PG2 as long, so we can store it directly as long, or we have to convert to enum before

```C#
case 104:    // property "encoding"
    accessor.TransferInteger(writeOperation, accessIndex, ref encoding);
    break;
...or...
case 104:    // property "encoding"
    long e;
    e = (long)encoding;
    accessor.TransferInteger(writeOperation, accessIndex, ref e);
    encoding = (pEncodingType)e;
    break;
```
The TransferValue depends on how we store the value

