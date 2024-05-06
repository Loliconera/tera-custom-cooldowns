﻿using System;
using System.Collections.Generic;

namespace TeraPacketParser.Messages;

public class S_REQUEST_CITY_WAR_MAP_INFO_DETAIL : ParsedMessage
{
    public readonly List<(uint Id, string Name)> GuildDetails;

    public S_REQUEST_CITY_WAR_MAP_INFO_DETAIL(TeraMessageReader reader) : base(reader)
    {
        GuildDetails = new List<(uint Id, string Name)>();
        try
        {
            var count = reader.ReadUInt16();
            if (count == 0) return;
            var offset = reader.ReadUInt16();
            reader.BaseStream.Position = offset - 4;
            for (var i = 0; i < count; i++)
            {
                reader.Skip(2); // var current = reader.ReadUInt16();
                var next = reader.ReadUInt16();
                reader.Skip(6);
                var id = reader.ReadUInt32();
                var name = reader.ReadTeraString();
                /*var gm = */reader.ReadTeraString();
                /*var logo = */reader.ReadTeraString();
                GuildDetails.Add((id, name));
                if (next != 0) reader.BaseStream.Position = next - 4;
            }
        }
        catch
        {
            // ignored
        }
    }
}