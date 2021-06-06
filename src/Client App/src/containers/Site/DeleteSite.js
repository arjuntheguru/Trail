import React from 'react';
import { deleteSite } from '../../store/site-actions';

import { useDispatch } from "react-redux";
import DeleteModal from '../../components/DeleteModal/DeleteModal';

const DeleteSite = ({ show, onClose, data }) => {

    const dispatch = useDispatch();

    const deleteSiteHandler = () => {
        console.log(data)
        dispatch(deleteSite(data));
        onClose();
    }

    return (
        <DeleteModal show={show} onClose={onClose} data={data.name} handler={deleteSiteHandler}/>
    )

}

export default DeleteSite;