LIST city_links = cheddar_hummous, hummous_tuna, cheddar_tuna

EXTERNAL get_links()

-> start

==start
 ~ temp links = get_links()
    {links ? city_links.cheddar_hummous:
        cheddar & hummous
    }
    {links ? city_links.hummous_tuna:
        hummous & tuna
    }
    {links ? city_links.cheddar_tuna:
        cheddar and tuna. Yuck
    }
-> DONE


====function GetLinks
~ return get_links()