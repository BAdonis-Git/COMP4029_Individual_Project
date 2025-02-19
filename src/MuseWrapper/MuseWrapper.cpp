#include "pch.h"
#include "MuseWrapper.h"

extern "C" {

void* GetMuseManager()
{
	return interaxon::bridge::MuseManagerWindows::getInstance().get();
}

}